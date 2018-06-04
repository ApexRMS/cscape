
using System;
using SyncroSim.Common;
using System.Globalization;

namespace SyncroSim.Circuitscape
{
    internal class InputFileMap
    {
        private SortedKeyMap2<InputFileRecord> m_Map = new SortedKeyMap2<InputFileRecord>(SearchMode.ExactPrev);

        public void AddInputFileRecord(Nullable<int> iteration, Nullable<int> timestep, string polygonFile, string habitatFile)
        {
            InputFileRecord r = this.m_Map.GetItemExact(iteration, timestep);

            if (r != null)
            {
                string s = string.Format(CultureInfo.InvariantCulture,
                    "There is already an input file record for iteration {0} and timestep {1}", iteration, timestep);

                throw new ArgumentException(s);
            }

            this.m_Map.AddItem(iteration, timestep, new InputFileRecord(polygonFile, habitatFile));
        }

        public InputFileRecord GetInputFileRecord(int iteration, int timestep)
        {
            return this.m_Map.GetItem(iteration, timestep);
        }
    }
}
