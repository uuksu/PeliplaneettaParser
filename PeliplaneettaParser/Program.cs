using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PeliplaneettaParser.Model;

namespace PeliplaneettaParser
{
    class Program
    {
        static void Main(string[] args)
        {
            string rootDirectory = args[0];
            string[] filePaths = Directory.GetFiles(rootDirectory, "*.html", SearchOption.AllDirectories);

            Console.WriteLine($"Starting to parse {filePaths.Length} files.");

            using (StreamWriter writer = new StreamWriter("log.txt", true))
            {
                using (PeliplaneettaContext context = new PeliplaneettaContext())
                {
                    // This speeds up saving but data needs to be validated
                    context.Configuration.ValidateOnSaveEnabled = false;
                    context.Configuration.AutoDetectChangesEnabled = false;

                    ThreadPageParser pageParser = new ThreadPageParser(context);
                    int fileCounter = 0;
                    int errorCounter = 0;

                    foreach (string filePath in filePaths)
                    {
                        try
                        {
                            pageParser.Parse(filePath);

                            Console.SetCursorPosition(0, 1);
                            Console.Write("                                          ");
                            Console.SetCursorPosition(0, 1);
                            Console.Write($"{fileCounter}/{filePaths.Length} Errors: {errorCounter}");
                        }
                        catch (Exception)
                        {
                            writer.WriteLine(filePath);
                            writer.AutoFlush = true;
                            errorCounter++;
                        }

                        fileCounter++;
                    }
                    
                }
            }
        }
    }
}
