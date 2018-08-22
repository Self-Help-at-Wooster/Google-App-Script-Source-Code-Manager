using AppScriptManager;
using System;
using System.IO;

namespace LibraryTester
{
    internal static class Program
    {

        private static void Main(string[] args)
        {
            var info = AppScriptSourceCodeManager.Initialize(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName).Result;
            Console.WriteLine(info.MyResult);

            if (info.IsSuccess)
            {
                try
                {
                    Console.WriteLine("Please wait... Creating a new Google App Script Project!");
                    AppScriptSourceCodeManager.CreateNewGASProject("Library Test Demo").Wait();
                }
                catch (AppScriptSourceCodeManager.InfoException ex)
                {
                    Console.WriteLine(ex);

                }

                Console.ReadLine();

                foreach (var str in AppScriptSourceCodeManager.GetScriptInfo())
                {
                    Console.WriteLine(str);
                }

            }
            Console.Read();
        }
    }
}
