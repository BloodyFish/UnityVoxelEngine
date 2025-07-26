using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Mathematics;

namespace Voxels.Scripts.Utils
{
    public class Performance
    {
        public const int ChunkGeneration = 0;
        public const int ChunkGreedyMeshing = 1;
        public const int ChunkGenerateMesh = 2;
        
        public class PerformanceEntry
        {
            public int Bucket { get; }
            private Stopwatch _activeStopwatch;
            
            public bool Active => _activeStopwatch?.IsRunning ?? false;

            public double timeMs { get; private set; }

            public PerformanceEntry(int bucket)
            {
                Bucket = bucket;
                _activeStopwatch = new Stopwatch();
                _activeStopwatch.Start();
            }

            public void End()
            {
                _activeStopwatch.Stop();
                timeMs = _activeStopwatch.Elapsed.TotalMilliseconds;
                Performance.End(this);
            }
        }

        // Metrics may be submitted from multiple threads, use threadsafe dictionary
        private static ConcurrentDictionary<int, PerformanceMetric> metrics = new();

        public static PerformanceEntry Begin(int bucket)
        {
            return new PerformanceEntry(bucket);
        }

        public static void Reset()
        {
            metrics.Clear();
        }

        public static PerformanceMetric GetMetric(int bucket)
        {
            metrics.TryGetValue(bucket, out var value);
            return value;
        }

        public static void End(PerformanceEntry entry)
        {
            if (entry.Active)
            {
                entry.End();
                return;
            }

            if (!metrics.TryGetValue(entry.Bucket, out var metric))
            {
                metric = new PerformanceMetric();
                metrics[entry.Bucket] = metric;
            }
            metric.Push(entry);
        }

        public class PerformanceMetric
        {
            private double max = 0;
            private double min = double.MaxValue;
            private double total = 0;
            private int count = 0;

            public double Max => max;
            public double Min => min;
            public double Mean => total / count;
            public double Total => total;
            
            public void Push(PerformanceEntry entry)
            {
                max = math.max(max, entry.timeMs);
                min = math.min(min, entry.timeMs);

                total += entry.timeMs;
                count++;
            }
        }
    }
}