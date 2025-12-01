using System;
using System.Collections.Generic;
using System.Text;

namespace ExperimentASR.Models
{
    public class BenchmarkResult
    {
        public string ModelName { get; set; }
        public double AverageWer { get; set; }
        public double AverageRtf { get; set; }
        public int TestsCount { get; set; }
    }
}
