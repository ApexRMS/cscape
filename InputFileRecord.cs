
namespace SyncroSim.Circuitscape
{
    class InputFileRecord
    {
        private string m_PolygonFile;
        private string m_HabitatFile;

        public InputFileRecord(string polygonFile, string habitatFile)
        {
            this.m_PolygonFile = polygonFile;
            this.m_HabitatFile = habitatFile;
        }

        public string HabitatFile
        {
            get
            {
                return m_HabitatFile;
            }
            set
            {
                m_HabitatFile = value;
            }
        }

        public string PolygonFile
        {
            get
            {
                return m_PolygonFile;
            }
            set
            {
                m_PolygonFile = value;
            }
        }
    }
}
