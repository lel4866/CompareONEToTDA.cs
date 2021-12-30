﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CompareOneToIB;

namespace CompareONEToIB;

internal static class CommandLine
{
    internal static void ProcessCommandLineArguments(string[] args)
    {
        string? arg_name = null;
        bool exit = false;
        bool symbol_specified = false;
        bool id_specified = false;
        bool if_specified = false;
        bool td_specified = false;
        bool tf_specified = false;
        bool od_specified = false;
        bool of_specified = false;
        string arg_with_backslash;

        foreach (string arg in args)
        {
            if (arg_name == null)
            {
                switch (arg)
                {
                    case "-v":
                    case "--version":
                        Console.WriteLine("CompareONEToIB version " + Program.version);
                        System.Environment.Exit(0);
                        break;
                    case "-s":
                    case "--symbol":
                        arg_name = "symbol";
                        if (symbol_specified)
                        {
                            Console.WriteLine("***Command Line Error*** symbol can only be specified once");
                            exit = true;
                        }
                        symbol_specified = true;
                        break;
                    case "-if":
                    case "--ibfile":
                        arg_name = "ibfile";
                        if (if_specified)
                        {
                            Console.WriteLine("***Command Line Error*** IB file can only be specified once");
                            exit = true;
                        }
                        if (id_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both an IB file and an IB directory");
                            exit = true;
                        }
                        if (td_specified || tf_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both IB and TDA files/directories");
                            exit = true;
                        }
                        if_specified = true;
                        break;
                    case "-id":
                    case "--ibdir":
                        arg_name = "ibdir";
                        if (id_specified)
                        {
                            Console.WriteLine("***Command Line Error*** IB directory can only be specified once");
                            exit = true;
                        }
                        if (if_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both IB file and IB directory");
                            exit = true;
                        }
                        if (td_specified || tf_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both IB and TDA files/directories");
                            exit = true;
                        }
                        id_specified = true;
                        break;
                    case "-tf":
                    case "--tdafile":
                        arg_name = "tdafile";
                        if (tf_specified)
                        {
                            Console.WriteLine("***Command Line Error*** IB file can only be specified once");
                            exit = true;
                        }
                        if (td_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both IB file and IB directory");
                            exit = true;
                        }
                        if (id_specified || if_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both IB and TDA files/directories");
                            exit = true;
                        }
                        NotImplementedYet("Comaprison to TD Ameritrade Position files not implemented yet");
                        Program.tda = tf_specified = true;
                        break;
                    case "-td":
                    case "--tdadir":
                        arg_name = "tdadir";
                        if (td_specified)
                        {
                            Console.WriteLine("***Command Line Error*** IB directory can only be specified once");
                            exit = true;
                        }
                        if (tf_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both IB file and IB directory");
                            exit = true;
                        }
                        if (id_specified || if_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both IB and TDA files/directories");
                            exit = true;
                        }
                        NotImplementedYet("Comaprison to TD Ameritrade Position files not implemented yet");
                        Program.tda = td_specified = true;
                        break;
                    case "-of":
                    case "--onefile":
                        arg_name = "onefile";
                        if (of_specified)
                        {
                            Console.WriteLine("***Command Line Error*** ONE file can only be specified once");
                            exit = true;
                        }
                        if (of_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both ONE file and ONE directory");
                            exit = true;
                        }
                        of_specified = true;
                        break;
                    case "-od":
                    case "--onedir":
                        arg_name = "onedir";
                        if (od_specified)
                        {
                            Console.WriteLine("***Command Line Error*** ONE directory can only be specified once");
                            exit = true;
                        }
                        if (of_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both ONE file and ONE directory");
                            exit = true;
                        }
                        od_specified = true;
                        break;
                    case "-h":
                    case "--help":
                        Console.WriteLine("CompareONEToIB version " + Program.version);
                        Console.WriteLine("Compare OptionnetExplorer positions with Interactive Brokers positions");
                        Console.WriteLine("Program will compare positions in the latest file in each of the specified directories");
                        Console.WriteLine("\nCommand line arguments:");
                        Console.WriteLine("    --version, -v : display version number");
                        Console.WriteLine("    --symbol, -s  : specify primary option index symbol; currently, SPX, RUT, or NDX");
                        Console.WriteLine("    --ibfile, -if  : specify file that contains files exported from IB (of form: portfolio.yyyymmdd.csv)");
                        Console.WriteLine("    --ibdir, -id  : specify directory that contains files exported from IB (of form: portfolio.yyyymmdd.csv)");
                        Console.WriteLine("    --tdafile, -tf  : specify file that contains files exported from TDA (of form: yyyy-mm-dd-PositionStatement.csv)");
                        Console.WriteLine("    --tdadir, -td  : specify directory that contains files exported from TDA (of form: yyyy-mm-dd-PositionStatement.csv)");
                        Console.WriteLine("    --onefile, -of  : specify file that contains files exported from ONE (of form: yyyy-mm-dd-ONEDetailReport.csv)");
                        Console.WriteLine("    --onedir, -od  : specify directory that contains files exported from ONE (of form: yyyy-mm-dd-ONEDetailReport.csv)");
                        Console.WriteLine("    --help, -h  : display command line argument information");
                        System.Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("***Command Line Error*** Invalid command line argument: " + arg + ". Program exiting.");
                        exit = true;
                        break;
                }
            }
            else
            {
                switch (arg_name)
                {
                    case "symbol":
                        string uc_arg = arg.ToUpper();
                        if (!Program.associated_symbols.ContainsKey(uc_arg))
                        {
                            Console.WriteLine("***Command Line Error*** Unknown symbol: " + uc_arg + ". Program exiting.");
                            System.Environment.Exit(-1);
                        }
                        Program.master_symbol = uc_arg;
                        break;

                    case "ibfile":
                        if (!File.Exists(arg))
                        {
                            if (Directory.Exists(arg))
                                Console.WriteLine("***Command Line Error*** specified IB File: " + arg + " is a directory, not a file. Program exiting.");
                            else
                                Console.WriteLine("***Command Line Error*** IB File: " + arg + " does not exist. Program exiting.");
                            exit = true;
                        }
                        Program.broker_filename = arg;
                        break;

                    case "ibdir":
                        if (!Directory.Exists(arg))
                        {
                            if (File.Exists(arg))
                                Console.WriteLine("***Command Line Error*** specified IB Directory: " + arg + " is a file, not a directory. Program exiting.");
                            else
                                Console.WriteLine("***Command Line Error*** IB Directory: " + arg + " does not exist. Program exiting.");
                            exit = true;
                        }
                        arg_with_backslash = arg;
                        if (!arg.EndsWith('\\'))
                            arg_with_backslash += '\\';
                        Program.broker_directory = arg_with_backslash;
                        break;

                    case "tdafile":
                        if (!File.Exists(arg))
                        {
                            if (Directory.Exists(arg))
                                Console.WriteLine("***Command Line Error*** specified TDA File: " + arg + " is a directory, not a file. Program exiting.");
                            else
                                Console.WriteLine("***Command Line Error*** TDA File: " + arg + " does not exist. Program exiting.");
                            exit = true;
                        }
                        Program.broker_filename = arg;
                        break;

                    case "tdadir":
                        if (!Directory.Exists(arg))
                        {
                            if (File.Exists(arg))
                                Console.WriteLine("***Command Line Error*** specified TDA Directory: " + arg + " is a file, not a directory. Program exiting.");
                            else
                                Console.WriteLine("***Command Line Error*** TDA Directory: " + arg + " does not exist. Program exiting.");
                            exit = true;
                        }
                        arg_with_backslash = arg;
                        if (!arg.EndsWith('\\'))
                            arg_with_backslash += '\\';
                        Program.broker_directory = arg_with_backslash;
                        break;

                    case "onefile":
                        if (!File.Exists(arg))
                        {
                            if (Directory.Exists(arg))
                                Console.WriteLine("***Command Line Error*** specified ONE File: " + arg + " is a directory, not a file. Program exiting.");
                            else
                                Console.WriteLine("***Command Line Error*** ONE File: " + arg + " does not exist. Program exiting.");
                            exit = true;
                        }
                        Program.one_filename = arg;
                        break;

                    case "onedir":
                        if (!Directory.Exists(arg))
                        {
                            if (File.Exists(arg))
                                Console.WriteLine("***Command Line Error*** specified ONE Directory: " + arg + " is a file, not a directory. Program exiting.");
                            else
                                Console.WriteLine("***Command Line Error*** ONE Directory: " + arg + " does not exist. Program exiting.");
                            exit = true;
                        }
                        arg_with_backslash = arg;
                        if (!arg.EndsWith('\\'))
                            arg_with_backslash += '\\';
                        Program.one_directory = arg_with_backslash;
                        break;
                }
                arg_name = null;
            }
        }

        if (exit)
            System.Environment.Exit(-1);

        if (!symbol_specified)
        {
            Console.WriteLine("***Command Line Error*** No symbol (--symbol) specified");
            exit = true;
        }

        if (!id_specified && !if_specified)
        {
            Console.WriteLine("***Command Line Error*** No IB file or directory (--ibdir) specified");
            exit = true;
        }

        if (!od_specified && !of_specified)
        {
            Console.WriteLine("***Command Line Error*** No ONE file or directory (--onedir) specified");
            exit = true;
        }

        if (exit)
            System.Environment.Exit(-1);
    }

    internal static void NotImplementedYet(string msg)
    {
        Console.WriteLine(msg);
        System.Environment.Exit(-1);
    }
}