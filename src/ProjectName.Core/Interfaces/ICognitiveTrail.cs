using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectName.Core.Interfaces
{
    public interface ICognitiveTrail
    {
        void Record(string phase, object data);
        string GetHistory();
    }
}
