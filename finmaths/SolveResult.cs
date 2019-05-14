using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace finmaths
{
    public class SolveResult
    {
        public double[] Weights { get; set; }
        public double TangentMean { get; set; }
        public double TangentVar { get; set; }
        public double[] FrontierMean { get; set; }
        public double[] FrontierVar { get; set; }
    }
}
