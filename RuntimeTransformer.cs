using System;
using System.IO;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using SyncroSim.Core;
using SyncroSim.StochasticTime;

namespace SyncroSim.Circuitscape
{
    class RuntimeTransformer : StochasticTimeTransformer
    {
        private string m_ExeName;
        private string m_InputFolder;
        private string m_OutputFolder;
        private DataSheet m_InputFiles;
        private InputFileMap m_InputFileMap;
        private const string BASE_EXE_NAME = "cs_run.exe";

        public override void Configure()
        {
            base.Configure();

            if (this.IsUserInteractive())
            {
                throw new InvalidOperationException("This model does not support interactive mode.");
            }

            DataSheet ds = this.ResultScenario.GetDataSheet("Circuitscape_RunControl");
            DataTable dt = ds.GetData();

            if (ds.GetDataRow() == null)
            {
                dt.Rows.Add(dt.NewRow());
                ds.Changes.Add(new ChangeRecord(this, "Added default run control data."));
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            this.InitializeExe();
            this.InitializeInputFiles();
        }

        private void InitializeExe()
        {
            this.m_ExeName = BASE_EXE_NAME;
            string SearchExe = this.GetExternalExecutableName("cs_run.exe", "Circuitscape");

            if (SearchExe != null)
            {
                this.m_ExeName = SearchExe;
            }
        }

        void InitializeInputFiles()
        {
            this.m_InputFileMap = new InputFileMap();
            this.m_InputFiles = this.ResultScenario.GetDataSheet("Circuitscape_InputFile");
            DataTable dt = this.m_InputFiles.GetData();

            if (dt.DefaultView.Count == 0)
            {
                throw new InvalidOperationException("No input files specified.  Cannot continue.");
            }

            foreach (DataRow dr in dt.Rows)
            {
                Nullable<int> Iteration = GetNullableInt(dr, "Iteration") ;
                Nullable<int> Timestep = GetNullableInt(dr, "Timestep");
                string PolygonFile = (string)dr["PolygonFile"];
                string HabitatFile = (string)dr["HabitatFile"];

                this.m_InputFileMap.AddInputFileRecord(Iteration, Timestep, PolygonFile, HabitatFile);
            }
        }

        protected override void OnTimestep(int iteration, int timestep)
        {
            base.OnTimestep(iteration, timestep);

            this.m_InputFolder = this.Library.CreateTempFolder("Circuitscape\\In", true);
            this.m_OutputFolder = this.Library.CreateTempFolder("Circuitscape\\Out", true);
            string PolygonFile = this.PrepareInputFile(iteration, timestep, true);
            string HabitatFile = this.PrepareInputFile(iteration, timestep, false);

            if (PolygonFile != null && HabitatFile != null)
            {
                string IniName = this.CreateCircuitscapeIniFile(iteration, timestep, PolygonFile, HabitatFile);
                base.ExternalTransform(this.m_ExeName, IniName, null, null);
                this.CopyOutputFiles();
                this.UpdateOutputDataSheet(iteration, timestep);
            }
        }

        private string GetInputFile(int iteration, int timestep, bool polygonFile)
        {
            InputFileRecord r = this.m_InputFileMap.GetInputFileRecord(iteration, timestep);

            if (r == null)
            {
                return null;
            }
            else
            {
                string folder = this.Library.GetFolderName(LibraryFolderType.Input, this.m_InputFiles, false);

                if (polygonFile)
                {
                    return Path.Combine(folder, r.PolygonFile);
                }
                else
                {
                    return Path.Combine(folder, r.HabitatFile);
                }
            }
        }

        private string PrepareInputFile(int iteration, int timestep, bool polygonFile)
        {
            string SourceFile = this.GetInputFile(iteration, timestep, polygonFile);

            if (SourceFile == null)
            {
                return null;
            }

            string TargetFileBase = Path.GetFileNameWithoutExtension(SourceFile) + ".asc";
            string TargetFileFull = Path.Combine(this.m_InputFolder, TargetFileBase);

            if (Path.GetExtension(SourceFile).ToLower() == ".tif")
            {
                if (!Translate.GdalTranslate(SourceFile, TargetFileFull, GdalOutputFormat.AAIGrid, GdalOutputType.Float64, GeoTiffCompressionType.None, null))
                {
                    throw new InvalidOperationException("Cannot translate from raster format: " + SourceFile);
                }
            }
            else
            {
                Debug.Assert(Path.GetExtension(SourceFile).ToLower() == ".asc");
                File.Copy(SourceFile, TargetFileFull);
            }

            return TargetFileFull;
        }

        private string CreateCircuitscapeIniFile(int iteration, int timestep, string polygonFile, string habitatFile)
        {
            string IniFileName = Path.Combine(this.m_InputFolder, "Circuitscape.ini");
            string OutFileName = Path.Combine(this.m_OutputFolder, FormatFileName("Circuitscape", iteration, timestep, "out"));

            using (StreamWriter t = new StreamWriter(IniFileName))
            {
                t.WriteLine("[circuitscape options]");
                t.WriteLine("data_type = raster");
                t.WriteLine("scenario = pairwise");
                t.WriteLine("write_cur_maps = TRUE");
                t.WriteLine("point_file = {0}", polygonFile);
                t.WriteLine("polygon_file = {0}", polygonFile);
                t.WriteLine("habitat_file = {0}", habitatFile);
                t.WriteLine("output_file = {0}", OutFileName);
            }

            return IniFileName;
        }

        private void CopyOutputFiles()
        {
            DataSheet ds = this.ResultScenario.GetDataSheet("Circuitscape_OutputFile");
            string OutFolderName = this.Library.GetFolderName(LibraryFolderType.Output, ds, true);

            foreach (string f in Directory.GetFiles(this.m_OutputFolder))
            {
                string t = Path.Combine(OutFolderName, Path.GetFileName(f));
                File.Copy(f, t);
            }
        }

        private void UpdateOutputDataSheet(int iteration, int timestep)
        {
            DataSheet ds = this.ResultScenario.GetDataSheet("Circuitscape_OutputFile");
            string OutFolderName = this.Library.GetFolderName(LibraryFolderType.Output, ds, true);
            string BaseName = string.Format(CultureInfo.InvariantCulture, "Circuitscape-It{0}-Ts{1}_cum_curmap", iteration, timestep);
            string AsciiName = Path.Combine(OutFolderName, BaseName + ".asc");
            string TifName = Path.Combine(OutFolderName, BaseName + ".tif");

            if (!Translate.GdalTranslate(AsciiName, TifName, GdalOutputFormat.GTiff, GdalOutputType.Float64, GeoTiffCompressionType.None, null))
            {
                throw new InvalidOperationException("Cannot translate from ASCII: " + AsciiName);
            }

            DataTable dt = ds.GetData();
            dt.Rows.Add(new object[] { iteration, timestep, Path.GetFileName(TifName) });
        }

        private static string FormatFileName(string prefix, int iteration, int timestep, string extension)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-It{1}-Ts{2}.{3}", prefix, iteration, timestep, extension);
        }

        private int? GetNullableInt(DataRow dr, string columnName)
        {
            object value = dr[columnName];

            if (object.ReferenceEquals(value, DBNull.Value) || object.ReferenceEquals(value, null))
            {
                return null;
            }
            else
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
        }
    }
}
