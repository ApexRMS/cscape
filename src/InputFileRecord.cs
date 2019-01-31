//cscape: A SyncroSim Package for running Circuitscape simulations.
//Copyright © 2007-2019 Apex Resource Management Solution Ltd. (ApexRMS). All rights reserved.

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
        }

        public string PolygonFile
        {
            get
            {
                return m_PolygonFile;
            }
        }
    }
}
