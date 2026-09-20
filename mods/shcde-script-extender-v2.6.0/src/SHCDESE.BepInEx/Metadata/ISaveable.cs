using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SHCDESE.Metadata
{
    /// <summary>
    /// Describes a class that wants to store information when the game saves
    /// </summary>
    public interface ISaveable
    {
        public void OnSave();
        public void OnLoad();
    }
}
