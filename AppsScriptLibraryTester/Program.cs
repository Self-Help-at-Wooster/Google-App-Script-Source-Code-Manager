using AppsScriptManager;
using System;
using System.IO;

namespace LibraryTester
{
    internal static class Program
    {

        private static void Main(string[] args)
        {
            var info = AppsScriptSourceCodeManager.Initialize(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName).Result;
            Console.WriteLine(info.MyResult);

            if (info.IsSuccess)
            {
                try
                {
                    Console.WriteLine("Please wait... Creating a new Google App Script Project!");
                    AppsScriptSourceCodeManager.CreateNewGASProject("Library Test Demo").Wait();
                }
                catch (AppsScriptSourceCodeManager.InfoException ex)
                {
                    Console.WriteLine(ex);

                }

                Console.ReadLine();

                foreach (var str in AppsScriptSourceCodeManager.GetScriptInfo())
                {
                    Console.WriteLine(str);
                }

            }
            Console.Read();
        }
    }
}
