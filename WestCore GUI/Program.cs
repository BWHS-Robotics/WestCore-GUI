﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WestCore_GUI.Charts;
using static WestCore_GUI.Form1;

namespace WestCore_GUI
{
    static class Program
    {
        public static LiveChart motors;
        public static bool setup = false;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Console.WriteLine("Creating Named Pipe...");

            // Create new pipe server
            // Parameters:
            // "west-pros-pipe" - name of pipe
            // PipeDirection.InOut - Allows data to be sent and received
            // 1 - Only 1 client will be able to connect at a time  
            // PipeTransmissionMode.Byte - Type of data being transfered
            var namedPipeServer = new NamedPipeServerStream("west-pros-pipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte);

            Console.WriteLine("west-core-pipe successfully initialized");

            Console.WriteLine("Waiting for connection...");



            // Wait until C++ client connects
            namedPipeServer.WaitForConnection();

            // The stream reader will be what reads the contents from the Named Pipe
            var streamReader = new BinaryReader(namedPipeServer);

            Console.WriteLine("Waiting for line...");

            Task.Run(() =>
            {
                bool chartBuilt = false;

                int currentFrame = 0;
                int frameIncrement = 20;

                while (true)
                {
                    if (Form1.pidChart == null) continue;

                    try
                    {

                        Console.WriteLine("waiting for line");
                        var len = (int)streamReader.ReadUInt32();
                        var li = new string(streamReader.ReadChars(len));

                        WestDebug.Log(Level.Info, li);
                        Console.WriteLine("found line " + li);

                        if (!li.StartsWith("GUI_DATA_8378")) continue;

                        li = li.Substring(13);

                        if (li.Length <= 1) continue;

                        li += ",";

                        string[] split = li.Split(',');

                        for (int i = 0; i < split.Length; i++)
                        {
                            Console.WriteLine("Found split " + split[i]);

                            if (split[i].Length <= 1) continue;

                            string[] variableSplit = split[i].Split('|');


                            for (int j = 1; j < variableSplit.Length; j++)
                            {
                                Console.WriteLine("Found varsplit " + variableSplit[i]);

                                if (variableSplit[j].Length <= 1) continue;

                                string[] valueSplit = variableSplit[j].Split('=');

                                if (!chartBuilt)
                                {
                                    Console.WriteLine("Adding series");
                                    motorsBuilder.AddSeries(variableSplit[0].Split('=')[1]);
                                }
                                else
                                {
                                    Console.WriteLine(valueSplit.ToString());
                                    Console.WriteLine($"Adding {(int)double.Parse(valueSplit[1])} at {i} at frame {currentFrame}");
                                    motors.AddPoint(i, currentFrame, (int)double.Parse(valueSplit[1]));
                                }
                            }
                        }

                        if (!chartBuilt)
                        {
                            motors = motorsBuilder.Build();
                            chartBuilt = true;


                        }

                        //Form1.pidChart.AddPoint(0, x, a);
                        //Form1.pidChart.AddPoint(1, x, b);
                        //Form1.pidChart.AddPoint(2, x, c);
                        //Form1.pidChart.AddPoint(3, x, d);

                        //x++;

                    }
                    catch (EndOfStreamException)
                    {
                        Console.WriteLine("Reached end of stream, ending...");
                        break;
                    }



                    currentFrame += frameIncrement;
                    //Console.WriteLine($"Read from pipe client: {streamReader.ReadLine()}");
                }

                // Lost connection, dispose of any ongoing streams and exit the program
                streamReader.Close();
                streamReader.Dispose();

                Application.Exit();
            });

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        static void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                Console.WriteLine(e.Data.ToString());
        }
    }
}
