using System;
using System.IO;
using System.Data;
using SyncroSim.Core;
using SyncroSim.StochasticTime;
using System.Globalization;

namespace SyncroSim.Circuitscape
{
    class RuntimeTransformer : Transformer
    {
        public override void Transform()
        {
            try
            {
                this.SetStatusMessage("Running Circuitscape");
                this.InternalTransform();
            }
            finally
            {
                this.SetStatusMessage(null);
            }
        }

        private void InternalTransform()
        {
            if (this.IsHeadlessInvocation())
            {
                string ExeName = this.GetCircuitscapeExeFile();
                string IniName = this.CreateCircuitscapeIniFile();

                this.InitRunControl();
                base.ExternalTransform(ExeName, IniName, null, null);

                this.CopyOutputFiles();
                this.AppendOutputDataSheet();
            }
            else
            {
                throw new InvalidOperationException("This model does not support interactive mode.");
            }
        }

        private void InitRunControl()
        {
            DataSheet ds = this.ResultScenario.GetDataSheet("Circuitscape_RunControl");
            DataTable dt = ds.GetData();

            if (ds.GetDataRow() == null)
            {
                dt.Rows.Add(dt.NewRow());
                ds.Changes.Add(new ChangeRecord(this, "Added default run control data."));
            }
        }

        private void CopyOutputFiles()
        {
            DataSheet ds = this.ResultScenario.GetDataSheet("Circuitscape_Output");
            string OutFolderName = this.Library.GetFolderName(LibraryFolderType.Output, ds, true);
            string TempFolderName = this.Library.GetTempFolderName("Circuitscape");

            foreach (string f in Directory.GetFiles(TempFolderName))
            {
                string t = Path.Combine(OutFolderName, Path.GetFileName(f));
                File.Copy(f, t);
            }
        }

        private void AppendOutputDataSheet()
        {
            int Iteration = Convert.ToInt32(this.GetRunControlValue("MaximumIteration"));
            int Timestep = Convert.ToInt32(this.GetRunControlValue("MaximumTimestep"));
            DataSheet ds = this.ResultScenario.GetDataSheet("Circuitscape_Output");
            string OutFolderName = this.Library.GetFolderName(LibraryFolderType.Output, ds, true);
            string OutASCIIName = Path.Combine(OutFolderName, "cc_cum_curmap.asc");
            string OutTIFBaseName = string.Format(CultureInfo.InvariantCulture, "cc-{0}-{1}.tif", Iteration, Timestep);
            string OutTIFFullName = Path.Combine(OutFolderName, OutTIFBaseName);

            if (!Translate.GdalTranslate(OutASCIIName, OutTIFFullName, GdalOutputFormat.GTiff, GdalOutputType.Float64, GeoTiffCompressionType.None, null))
            {
                throw new InvalidOperationException("Cannot translate from ASCII: " + OutASCIIName);
            }

            DataTable dt = ds.GetData();
            dt.Rows.Add(new object[] { Iteration, Timestep, OutTIFBaseName });
        }

        private bool IsHeadlessInvocation()
        {
            DataRow dr = this.GetEnvironmentRow("SSIM_USER_INTERACTIVE");

            if (dr == null)
            {
                return true;
            }
            else
            {
                return ((string)dr["Value"] != "True");
            }
        }

        private object GetRunControlValue(string columnName)
        {
            DataRow dr = this.ResultScenario.GetDataSheet("Circuitscape_RunControl").GetDataRow();
            return dr[columnName];
        }

        private object GetInputValue(string columnName)
        {
            DataRow dr = this.ResultScenario.GetDataSheet("Circuitscape_Input").GetDataRow();

            if (dr == null || dr[columnName] == DBNull.Value)
            {
                throw new ArgumentException("The input data is missing for: " + columnName);
            }

            return dr[columnName];
        }

        private string GetInputFileName(string fileName)
        {
            DataSheet ds = this.ResultScenario.GetDataSheet("Circuitscape_Input");
            string f = this.Library.GetFolderName(LibraryFolderType.Input, ds, false);

            return (Path.Combine(f, fileName));
        }

        private string CreateCircuitscapeIniFile()
        {
            string PolygonFile = this.GetInputFileName((string) this.GetInputValue("PolygonFile"));
            string HabitatFile = this.GetInputFileName((string)this.GetInputValue("HabitatFile"));
            string TempFolderName = this.Library.GetTempFolderName("Circuitscape");
            string IniFileName = Path.Combine(TempFolderName, "cc.ini");
            string OutFileName = Path.Combine(TempFolderName, "cc.out");

            using (StreamWriter t = new StreamWriter(IniFileName))
            {
                t.WriteLine("[circuitscape options]");
                t.WriteLine("data_type = raster");
                t.WriteLine("scenario = pairwise");
                t.WriteLine("write_cur_maps = TRUE");
                t.WriteLine("point_file = {0}", PolygonFile);
                t.WriteLine("polygon_file = {0}", PolygonFile);
                t.WriteLine("habitat_file = {0}", HabitatFile);
                t.WriteLine("output_file = {0}", OutFileName);                                                      
            }

            return IniFileName;
        }

        private string GetCircuitscapeExeFile()
        {
            string f = this.GetExeNameFromConfig();

            if (f == null)
            {
                f = this.GetDefaultExeName();
            }

            if (f == null)
            {
                throw new InvalidOperationException("Cannot find the Circuitscape EXE file!");
            }

            if (!File.Exists(f))
            {
                string s = string.Format(CultureInfo.InvariantCulture, "The Circuitscape EXE file does not exist: {0}", f);
                throw new InvalidOperationException(s);
            }

            return f;
        }

        private string GetExeNameFromConfig()
        {
            DataRow dr = this.GetEnvironmentRow("SSIM_WINDOWS_EXECUTABLE_LOCATION");

            if (dr == null)
            {
                return null;
            }

            string v = (string)dr["Value"];
            return v;
        }

        private string GetDefaultExeName()
        {
            string f = this.GetDefaultCircuitscapeFolder();
            return Path.Combine(f, "cs_run.exe");
        }

        private string GetDefaultCircuitscapeFolder()
        {
            string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Circuitscape");

            if (Directory.Exists(f))
            {
                return f;
            }

            f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Circuitscape");

            if (Directory.Exists(f))
            {
                return f;
            }

            //We only ship x64 builds, but it is nice to have this work on an x86
            //development machine, so:

            f = Path.Combine("C:\\Program Files", "Circuitscape");

            if (Directory.Exists(f))
            {
                return f;
            }

            f = Path.Combine("C:\\Program Files (x86)", "Circuitscape");

            if (Directory.Exists(f))
            {
                return f;
            }

            return null;
        }

        private DataRow GetEnvironmentRow(string keyName)
        {
            DataSheet ds = this.Library.GetDataSheet("Circuitscape_Environment");

            foreach (DataRow dr in ds.GetData().Rows)
            {
                if (dr.RowState == DataRowState.Deleted)
                {
                    continue;
                }

                if (dr["Name"] != DBNull.Value)
                {
                    string n = (string)dr["Name"];

                    if (n == keyName)
                    {
                        return dr;
                    }
                }
            }

            return null;
        }
    }
}
