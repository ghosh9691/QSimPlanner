using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QSP.RouteFinding.Containers;
namespace UnitTest
{

    [TestClass()]
    public class WptListLoadTest
    {

        //<TestMethod()> Public Sub WptListLoadOldMethodTest()

        //    'Dim sw As New Stopwatch
        //    'sw.Start()
        //    'DatabaseLoader.loadWptList("F:\FSX\aerosoft\Airbus_Fallback\Navigraph\ats.txt")
        //    'sw.Stop()

        //    'Debug.WriteLine("Took {0} ms.", sw.ElapsedMilliseconds)

        //End Sub

        [TestMethod()]

        public void WptListLoadNewMethodTest()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            TrackedWptList t = new TrackedWptList();
            new AtsFileLoader(t).ReadAtsFromFile("F:\\FSX\\aerosoft\\Airbus_Fallback\\Navigraph\\ats.txt");
            sw.Stop();

            Debug.WriteLine("Took {0} ms.", sw.ElapsedMilliseconds);

        }

    }
}