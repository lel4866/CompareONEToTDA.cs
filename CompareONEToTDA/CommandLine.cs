namespace CompareONEToBroker;

internal static class CommandLine
{
    const string VSDebugDir = @"C:\Users\lel48\VisualStudioProjects\CompareONEToTDA.cs\CompareONEToTDA\bin\Debug\net6.0";
    const string VSReleaseDir = @"C:\Users\lel48\VisualStudioProjects\CompareONEToTDA.cs\CompareONEToTDA\bin\Release\net6.0";
    const string VSProjectDir = @"C:\Users\lel48\VisualStudioProjects\CompareONEToTDA.cs";

    internal static void ProcessCommandLineArguments(string[] args)
    {
        string? arg_name = null;
        bool exit = false;
        bool symbol_specified = false;
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
                        Console.WriteLine("CompareONEToTDA version " + ONE.version);
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
                    case "-tf":
                    case "--tdafile":
                        arg_name = "tdafile";
                        if (tf_specified)
                        {
                            Console.WriteLine("***Command Line Error*** TDA file can only be specified once");
                            exit = true;
                        }
                        if (td_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both TDA file and TDA directory");
                            exit = true;
                        }
                        if (td_specified || tf_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both TDA and TDA files/directories");
                            exit = true;
                        }
                        tf_specified = true;
                        break;
                    case "-td":
                    case "--tdadir":
                        arg_name = "tdadir";
                        if (td_specified)
                        {
                            Console.WriteLine("***Command Line Error*** TDA directory can only be specified once");
                            exit = true;
                        }
                        if (tf_specified)
                        {
                            Console.WriteLine("***Command Line Error*** You cannot specify both TDA file and TDA directory");
                            exit = true;
                        }
                        td_specified = true;
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
                        Console.WriteLine("CompareONEToTDA version " + ONE.version);
                        Console.WriteLine("Compare OptionnetExplorer positions with TD Ameritrade positions");
                        Console.WriteLine("Program will compare positions in the latest file in each of the specified directories");
                        Console.WriteLine("\nCommand line arguments:");
                        Console.WriteLine("    --version, -v : display version number");
                        Console.WriteLine("    --symbol, -s  : specify primary option index symbol; currently, SPX, RUT, or NDX");
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
                        if (!ONE.associated_symbols.ContainsKey(uc_arg))
                        {
                            Console.WriteLine("***Command Line Error*** Unknown symbol: " + uc_arg + ". Program exiting.");
                            System.Environment.Exit(-1);
                        }
                        ONE.master_symbol = uc_arg;
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
                        ONE.broker_filename = arg;
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
                        ONE.broker_directory = arg_with_backslash;
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
                        ONE.one_filename = arg;
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
                        ONE.one_directory = arg_with_backslash;
                        break;
                }
                arg_name = null;
            }
        }

        if (exit)
            System.Environment.Exit(-1);

        if (!symbol_specified)
        {
            // default is spx
            ONE.master_symbol = "SPX";
        }

        if (!td_specified && !tf_specified)
        {
            // check if default TDA directory exists; default name and location is cwd/TDAExport/
            string curdir = Directory.GetCurrentDirectory();
            if (curdir == VSDebugDir || curdir == VSReleaseDir)
                curdir = VSProjectDir;
            curdir = Path.GetFullPath(curdir + "/TDAExport/"); // use GetFullPath to get "normalized" directory path
            if (Directory.Exists(curdir))
                ONE.broker_directory = curdir;
            else
            {
                Console.WriteLine("***Command Line Error*** No TDA file (--tdafile) or directory (--tdadir) specified, and default directory (cwd/TDAExport) doesn't exist");
                exit = true;
            }
        }

        if (!od_specified && !of_specified)
        {
            // check if default ONE directory exists; default name and location is cwd/ONEExport/
            string curdir = Directory.GetCurrentDirectory();
            if (curdir == VSDebugDir || curdir == VSReleaseDir)
                curdir = VSProjectDir;
            curdir = Path.GetFullPath(curdir + "/ONEExport/"); // use GetFullPath to get "normalized" directory path
            if (Directory.Exists(curdir))
                ONE.one_directory = curdir;
            else
            {
                Console.WriteLine("***Command Line Error*** No ONE file (--onefile) or directory (--onedir) specified, and default directory (cwd/ONEExport) doesn't exist");
                exit = true;
            }
        }

        if (exit)
            System.Environment.Exit(-1);
    }
}
