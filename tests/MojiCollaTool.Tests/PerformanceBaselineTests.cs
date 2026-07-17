using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class PerformanceBaselineTests
{
    private const int Iterations = 9;
    private const double P95ThresholdMilliseconds = 500;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PerformanceFixtureLoadP95IsWithinThreshold()
    {
        var archivePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "performance-mctzip.mctzip");
        var samples = new List<double>(Iterations);

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var extractionPath = Path.Combine(Path.GetTempPath(), "MojiCollaTool.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractionPath);

            try
            {
                var stopwatch = Stopwatch.StartNew();
                ZipFile.ExtractToDirectory(archivePath, extractionPath);
                var canvas = DataIO.ReadCanvasData(extractionPath);
                var mojiDatas = DataIO.ReadMojiDatas(extractionPath);
                stopwatch.Stop();

                Assert.AreEqual(LocatePosition.Right, canvas.Image2LocatePosition);
                Assert.AreEqual(3, mojiDatas.Count);
                Assert.IsTrue(mojiDatas.All(data => data.BorderBlurrRadius > 0));
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            finally
            {
                if (Directory.Exists(extractionPath))
                {
                    Directory.Delete(extractionPath, recursive: true);
                }
            }
        }

        samples.Sort();
        var p95Index = Math.Min(samples.Count - 1, (int)Math.Ceiling(samples.Count * 0.95) - 1);
        var p95Milliseconds = samples[p95Index];
        TestContext.WriteLine($"fixture=performance-mctzip.mctzip; iterations={Iterations}; p95_ms={p95Milliseconds:F3}; threshold_ms={P95ThresholdMilliseconds:F0}");

        Assert.IsTrue(
            p95Milliseconds <= P95ThresholdMilliseconds,
            $"Performance fixture p95 was {p95Milliseconds:F3} ms; threshold is {P95ThresholdMilliseconds:F0} ms.");
    }
}
