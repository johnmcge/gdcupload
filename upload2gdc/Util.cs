﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace upload2gdc
{
    class Util
    {
        public static int GoFindDataFiles(string basePath)
        {
            int numFilesNotFound = 0;
            Dictionary<int, SeqFileInfo> newSeqDataFiles = new Dictionary<int, SeqFileInfo>();

            // since we cannot modify a Dictionary item while iterating over the dictionary,
            // copy the keys to a List and iterate over that instead
            List<int> ListOfKeys = new List<int>();
            foreach (KeyValuePair<int, SeqFileInfo> dataFile in Program.SeqDataFiles)
            {
                ListOfKeys.Add(dataFile.Key);
            }

            foreach (int key in ListOfKeys)
            {
                string TracSeqDeliveryFolderName = "";

                SeqFileInfo newDataFile = Program.SeqDataFiles[key];
                string runId = newDataFile.Submitter_id.Substring(0, 35);  // first 35 chars of the submitter_id is our run_id

                if (newDataFile.DataFileName.IndexOf("bam") != -1)
                    TracSeqDeliveryFolderName = "uBam";

                else if (newDataFile.DataFileName.IndexOf("fastq") != -1)
                    TracSeqDeliveryFolderName = "fastq";

                string fileLocation = Path.Combine(basePath, TracSeqDeliveryFolderName, runId);

                if (File.Exists(Path.Combine(fileLocation, newDataFile.DataFileName)))
                {
                    newDataFile.DataFileLocation = fileLocation;
                    newDataFile.ReadyForUpload = true;
                    Program.SeqDataFiles[key] = newDataFile;
                }
                else
                {
                    numFilesNotFound++;
                }
            }

            return numFilesNotFound;
        }

        public static bool ProcessGDCMetaDataFile(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Console.WriteLine("File not found, GDC Metadata File: " + fileName);
                return false;
            }

            string jsonstring = "";

            try
            {
                jsonstring = File.ReadAllText(fileName);
            }
            catch
            {
                Console.WriteLine("Exception reading GDC Metadata File: " + fileName);
                return false;
            }

            if (!GDCmetadata.LoadGDCJsonObjects(jsonstring))
            {
                Console.WriteLine("Error loading GDC Metadata File: " + fileName);
                return false;
            }

            return true;
        }

        public static bool ProcessGDCUploadReport(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Console.WriteLine("File not found, Upload Report file from GDC: " + fileName);
                return false;
            }

            int counter = 0;
            string line;

            try
            {
                using (StreamReader file = new StreamReader(fileName))
                {
                    while ((line = file.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length > 1)
                        {
                            if (parts[2] == "submitted_unaligned_reads")
                            {
                                counter++;
                                SeqFileInfo newDataFile = new SeqFileInfo
                                {
                                    Id = parts[0],
                                    Related_case = parts[1],
                                    EType = parts[2],
                                    Submitter_id = parts[4],
                                    ReadyForUpload = false
                                };

                                var tempSUR = new SUR();
                                if (GDCmetadata.SURdictionary.TryGetValue(parts[4], out tempSUR))
                                {
                                    newDataFile.DataFileName = tempSUR.file_name;
                                    newDataFile.DataFileSize = tempSUR.file_size;
                                }

                                Program.SeqDataFiles.Add(counter, newDataFile);
                            }
                        }
                    }
                    file.Close();
                }
            }
            catch
            {
                Console.WriteLine("Exception while processing upload report from the gdc: " + fileName);
                Console.WriteLine("Counter = " + counter.ToString());
                return false;
            }
            return true;
        }


        public static void CheckLogFiles(string dirLocation)
        {
            string logfiledirmask = "*.log";

            string[] files = Directory.GetFiles(dirLocation, logfiledirmask, SearchOption.TopDirectoryOnly);

            List<string> CompletedUUIDs = new List<string>();
            List<string> FailedUUIDs = new List<string>();
            int TotalRequeues = 0;

            if (files.Length > 0)
            {
                string line = "";
                foreach (string filename in files)
                {
                    using (StreamReader file = new StreamReader(filename))
                    {
                        while ((line = file.ReadLine()) != null)
                        {
                            if (line.Contains("Multipart upload finished for file"))
                            {
                                string[] parts = line.Split();
                                CompletedUUIDs.Add(parts[5]);
                            }
                            else if (line.Contains("File-NOT-UPLOADED:"))
                            {
                                string[] parts = line.Split();
                                FailedUUIDs.Add(parts[5]);
                            }
                            else if (line.Contains("Re-queued:"))
                            {
                                TotalRequeues++;
                            }
                        }
                    }
                }
            }

            StringBuilder sb = new StringBuilder();

            sb.Append($"   Log File Scan Results: {DateTime.Now.ToString("g")} ");
            sb.Append(Environment.NewLine);
            sb.Append($"Total number of requeues: {TotalRequeues}");
            sb.Append(Environment.NewLine + Environment.NewLine);

            sb.Append($"*** Successfully uploaded: {CompletedUUIDs.Count()} ");
            sb.Append(Environment.NewLine);
            foreach (string item in CompletedUUIDs)
            {
                sb.Append(item);
                sb.Append(Environment.NewLine);
            }

            sb.Append(Environment.NewLine + Environment.NewLine);
            sb.Append($"*** Failed to upload: {FailedUUIDs.Count()} " + Environment.NewLine);
            foreach (string item in FailedUUIDs)
            {
                sb.Append(item);
                sb.Append(Environment.NewLine);
            }

            string resultsFileName = "logScan-" + DateTime.Now.ToString("yyyyddhhmmss") + ".log";

            try
            {
                File.WriteAllText(Path.Combine(dirLocation, resultsFileName), sb.ToString());
            }
            catch {
                Console.WriteLine("Exception writing results from log file scan.");
            }

            sb.Clear();
            Console.WriteLine($" Successfully uploaded: {CompletedUUIDs.Count()}" );
            Console.WriteLine($"          Faild upload: {FailedUUIDs.Count()}");
            Console.WriteLine($"        Total Requeues: {TotalRequeues}");
        }



    }
}
