using System.Diagnostics;
using CompareONEToBroker;

namespace CompareONEToTDA;

static class Program
{
    // used to parse option spec
    readonly internal static Dictionary<string, int> monthDict = new()
    {
        { "JAN", 1 },
        { "FEB", 2 },
        { "MAR", 3 },
        { "APR", 4 },
        { "MAY", 5 },
        { "JUN", 6 },
        { "JUL", 7 },
        { "AUG", 8 },
        { "SEP", 9 },
        { "OCT", 10 },
        { "NOV", 11 },
        { "DEC", 12 }
    };

    static int Main(string[] args)
    {
#if false
        ONE.RunUnitTests();
#endif

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        ONE.program_name = "CompareONEToTDA";
        ONE.version = "0.0.3";
        ONE.version_date = "2022-02-09";

        ONE.broker = "TDA";
        ONE.broker_directory = @"C:\Users\lel48\OneDrive\Documents\TDAExport\";

        ONE.ProcessCommandLine(args); // calls System.Environment.Exit(-1) if bad command line arguments

        (ONE.broker_filename, ONE.broker_filedate) = GetTDAFileName(ONE.broker_directory, ONE.broker_filename); // parses tda_filename from command line if it is not null to get date
        if (ONE.one_filename == null || ONE.broker_filename == null)
            return -1;

        Console.WriteLine("\nProcessing ONE file: " + ONE.one_filename);
        Console.WriteLine($"Processing {ONE.broker} file: {ONE.broker_filename}");

        bool rc = ONE.ProcessONEFile(ONE.one_filename);
        if (!rc)
            return -1;

        rc = ProcessTDAFile(ONE.broker_filename);
        if (!rc)
            return -1;

        rc = ONE.CompareONEPositionsToBrokerPositions();
        if (!rc)
            return -1;

        stopWatch.Stop();
        Console.WriteLine($"\nElapsed time = {stopWatch.Elapsed}");

        return 0;
    }

    static (string?, DateOnly) GetTDAFileName(string directory, string? specified_full_filename)
    {
        bool rc;
        const string filename_pattern = "????-??-??-PositionStatement.csv"; // file names look like: yyyy-mm-dd-PositionStatement.csv
        string[] files;
        DateOnly latestDate = new(1000, 1, 1);
        string latest_full_filename = "";
        string filename, datestr;

        if (specified_full_filename == null)
        {
            if (!Directory.Exists(directory))
            {
                Console.WriteLine($"\n***Error*** Specified TDA directory {directory} does not exist");
                return (null, latestDate);
            }

            files = Directory.GetFiles(ONE.broker_directory, filename_pattern, SearchOption.TopDirectoryOnly);
            bool file_found = false;
            foreach (string full_filename in files)
            {
                filename = Path.GetFileName(full_filename);
                datestr = filename[..10]; // yyyy-mm-dd
                rc = DateOnly.TryParse(datestr, out DateOnly dt);
                if (!rc)
                    continue;
                file_found = true;

                if (dt > latestDate)
                {
                    latestDate = dt;
                    latest_full_filename = full_filename;
                }
            }

            if (!file_found)
            {
                Console.WriteLine($"\n***Error*** No TDA Position files found in {directory} with following filename pattern: yyyy-mm--dd-PositionStatement.csv");
                return (null, latestDate);
            }

            return (latest_full_filename, latestDate);
        }

        if (!File.Exists(specified_full_filename))
        {
            Console.WriteLine($"\n***Error*** Specified TDA file {specified_full_filename} does not exist");
            return (null, latestDate);
        }

        filename = Path.GetFileName(specified_full_filename); // this is filename portion of full filename
        datestr = filename[..10];
        rc = DateOnly.TryParse(datestr, out latestDate); // day of expiration will be incorrect (it will be 1)
        if (!rc)
        {
            Console.WriteLine($"\n***Error*** Specified TDA file has invalid date: {datestr}");
            return (null, latestDate);
        }

        return (specified_full_filename, latestDate);
    }

    // TDA file looks like (blank lines inserted for clarity):
    //Position Statement for D-16758452 (margin) on 12/29/21 11:49:04
    //
    //None
    //Instrument, Qty, Days, Trade Price,Mark,Mrk Chng, P/L Open, P/L Day, BP Effect

    ///MES,+1,,4777.50,4776.25,-2.25,($6.25),($6.25),"($1,265.00)"
    //"Micro E-mini S&P 500, Mar-22 (prev. /MESH2)",+1,79,4777.50,4776.25,-2.25,($6.25),($6.25),

    //SPX,,,,,,($330.00),($330.00),$0.00
    //S&P 500 INDEX,0,,.00,4784.37,-1.98,$0.00,$0.00,
    //100 21 JAN 22 4795 PUT,+1,22,63.50,63.20,N/A,($30.00),($30.00),
    //100 (Quarterlys) 31 MAR 22 4795 CALL,+2,92,141.30,140.25,-4.85,($210.00),($210.00),
    //100 (Weeklys) 31 MAY 22 4650 PUT,-3,153,176.10,179.90,N/A,"($1,140.00)","($1,140.00)",

    //SPY,,,,,,$97.00,$97.00,"$23,828.00"
    //SPDR S&P500 ETF TRUST TR UNIT ETF,+100,,476.74,476.56,-.31,($18.00),($18.00),
    //100 18 JAN 22 477 PUT,+10,20,5.25,5.365,+.285,$115.00,$115.00,

    //...
    //...
    //Cash & Sweep Vehicle

    // each TDA position is an index, stock or futures, and consists of 2 or more lines.
    // option positions on each of these 3 types are lines starting with "100 " that follow the 2 lines defining the main position
    // futures positions are distinguished because the symbol in the first line starts with '/'
    // index positions are distinguished from stock positions primarily by the symbol in the first line, but also because the
    // quantity in the first line is 0 for an index

    //
    // TODO: throw away irrelevant positions
    //
    static internal bool ProcessTDAFile(string full_filename)
    {
        // sets line_index to index of header; throws away all lines after first blank line after header
        List<string> lines = ReadAllRelevantLines(full_filename, out int line_index);
        if (lines.Count < 2)
        {
            Console.WriteLine("\n***Error*** TDA File contains no positions.");
            return false;
        }

        // check header for required columns and get index of last required column
        string[] required_columns = { "Instrument", "Qty" };
        string header = lines[line_index++]; // get header
        string[] column_names = header.Split(',');
        for (int i = 0; i < column_names.Length; i++)
        {
            string column_name = column_names[i].Trim();
            if (column_name.Length > 0)
                ONE.broker_columns.Add(column_name, i);
        }
        for (int i = 0; i < required_columns.Length; i++)
        {
            if (!ONE.broker_columns.TryGetValue(required_columns[i], out int colnum))
            {
                Console.WriteLine($"\n***Error*** TDA file header must contain column named {required_columns[i]}");
                return false;
            }
            ONE.index_of_last_required_column = Math.Max(colnum, ONE.index_of_last_required_column);
        }
        ONE.broker_description_col = ONE.broker_columns["Instrument"];
        ONE.broker_quantity_col = ONE.broker_columns["Qty"];

        // now process each TDA position (stock, index, or futures), each consisting of 2 lines plus option lines starting with "100 "
        string line;
        bool irrelevant_position = false;
        while (line_index < lines.Count)
        {
            line = lines[line_index++];
            bool rc = ONE.ParseCSVLine(line, out List<string> fields);
            if (!rc) return false;

            if (fields.Count < ONE.index_of_last_required_column + 1)
            {
                Console.WriteLine($"\n***Error*** In TDA file, at line {line_index}, first line of position must have {ONE.index_of_last_required_column + 1} fields, not {fields.Count}");
                return false;
            }

            if (line_index == lines.Count)
            {
                Console.WriteLine($"\n***Error*** In TDA file, at line {line_index}, each position must consist of at least 2 lines in file, not just 1: {line}");
                return false;
            }

            // parse a new position - each position contains 2 or more lines
            // the first line has the following fields: Instrument,Qty,Days,Trade Price, Mark, Mrk Chng,P/L Open, P/L Day, BP Effect 
            // We only use the first field...Instrument to determine instrument type and symbol. All other info comes from the second line

            // get symbol from first line
            string symbol = fields[ONE.broker_description_col];

            string line2 = lines[line_index++];
            rc = ONE.ParseCSVLine(line2, out List<string> fields2);
            if (!rc) return false;

            if (fields2.Count < ONE.index_of_last_required_column + 1)
            {
                Console.WriteLine($"\n***Error*** In TDA file, at line {line_index}, second line of position must have {ONE.index_of_last_required_column + 1} fields, not {fields2.Count}");
                return false;
            }
            string symbol2   = fields2[ONE.broker_description_col];

            // get quantity from second line because for index and stocks, quantity in first line is 0 (quantity for index is always 0 in both lines)
            rc = int.TryParse(fields2[ONE.broker_quantity_col], out int quantity);
            if (!rc)
            {
                Console.WriteLine($"***Error*** In TDA file, in line {line_index}: invalid Quantity: {fields2[ONE.broker_quantity_col]}");
                return false;
            }

            if (symbol == ONE.master_symbol)
            {
                // this is the index position we want to check

                //SPX,,,,,,($330.00),($330.00),$0.00
                //S&P 500 INDEX,0,,.00,4784.37,-1.98,$0.00,$0.00,
                //100 21 JAN 22 4795 PUT,+1,22,63.50,63.20,N/A,($30.00),($30.00),
                //100 (Quarterlys) 31 MAR 22 4795 CALL,+2,92,141.30,140.25,-4.85,($210.00),($210.00),
                //100 (Weeklys) 31 MAY 22 4650 PUT,-3,153,176.10,179.90,N/A,"($1,140.00)","($1,140.00)",
                if (quantity != 0)
                {
                    Console.WriteLine($"\n***Error*** In TDA file, line {line_index}, specified quantity for INDEX must be 0, not: {quantity}");
                    return false;
                }

                // get options asociated with this index: parse following lines that start with "100 "
                int num_options = 0;
                while (line_index < lines.Count)
                {
                    line = lines[line_index++]; // use line here because this line might be first line of next position
                    var tdaPosition = new Position(isONEPosition: false) { Symbol = symbol };
                    int irc = ParseTDAOptionLine(line_index-1, line, ref tdaPosition); // fills in securityType, expiration, strike, and quantity
                    if (irc < 0)
                        return false; // line has invalid syntax; ParseTDAOptionLine() has already display error message
                    if (irc > 0)
                    {
                        --line_index; // reprocess this line
                        break; // line isn't an option position (doesn't start with "100 "; must be start of next position
                    }
                    if (tdaPosition.Quantity == 0)
                        continue; // ignore option positions with 0 quantity

                    // add new option to tdaPositions collection
                    rc = tdaPosition.Add(line_index-1, ONE.brokerPositions);
                    if (!rc)
                        return false;

                    num_options++;
                }

                if (num_options == 0)
                {
                    Console.WriteLine($"\n***Error*** In TDA file, line {line_index - 2} specifies {ONE.master_symbol} index, but no option position lines follow (lines that start with \"100 \").");
                    return false;
                }
            }
            else if (symbol.StartsWith('/'))
            {
                // This is futures symbol: it is only relevant if it is one of the futures associated with index we are checking;
                // All options on this future are ignored
                ///MES,+1,,4777.50,4776.25,-2.25,($6.25),($6.25),"($1,265.00)"
                //"Micro E-mini S&P 500, Mar-22 (prev. /MESH2)",+1,79,4777.50,4776.25,-2.25,($6.25),($6.25),
                if (quantity <= 0)
                {
                    Console.WriteLine($"\n***Error*** In TDA file, line {line_index - 1} specifies a futures position  in {symbol} with quantity {quantity}: {line}");
                    return false;
                }

                string futures_root = symbol[1..];
                Position tdaPosition = new(false) { Symbol = futures_root, Type = SecurityType.Futures, Quantity = quantity };
                if (ONE.associated_symbols[ONE.master_symbol].ContainsKey(futures_root))
                {
                    // get futures expiration
                    string[] futures_fields = symbol2.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    string futures_date_str = futures_fields[1].Trim()[0..6];
                    rc = DateOnly.TryParseExact(futures_date_str, "MMM-yy", out DateOnly expiration);
                    if (!rc)
                    {
                        Console.WriteLine($"\n***Error*** In TDA file, line {line_index - 1} has invalid futures contract expiration: {futures_date_str}");
                        return false;
                    }
                    tdaPosition.Expiration = expiration;
                    tdaPosition.Add(line_index, ONE.brokerPositions);
                }
                else
                    tdaPosition.Add(line_index, ONE.irrelevantBrokerPositions);

                // ignore any following option positions (which would be futures options)
                rc = IgnoreTDAOptionLines(symbol, lines, ref line_index);
                if (!rc)
                    return false;
            }
            else
            {
                // this is a stock or, an index unrelated to the index we are checking
                // if it is a stock, it is only relevant if it is one of the stocks associated with index we are checking
                // if it is an index, by this point we know it is not the index we are checking and it is therefore unrelated
                // the way we know it is a stock or index is that the quantity in the 2nd line is 0 if it is an index
                // All options on this stock or index are ignored

                //SPY,,,,,,$97.00,$97.00,"$23,828.00"
                //SPDR S&P500 ETF TRUST TR UNIT ETF,+100,,476.74,476.56,-.31,($18.00),($18.00),
                //100 18 JAN 22 477 PUT,+10,20,5.25,5.365,+.285,$115.00,$115.00,
                if (quantity == 0)
                    return true; // ignore stock positions with 0 quantity

                Position tdaPosition = new(false) { Symbol = symbol, Type = SecurityType.Stock, Quantity = quantity };
                if (ONE.associated_symbols[ONE.master_symbol].ContainsKey(symbol))
                    tdaPosition.Add(line_index, ONE.brokerPositions);
                else
                    tdaPosition.Add(line_index, ONE.irrelevantBrokerPositions);

                // ignore options on stock (or unrelated index)
                line_index++; // start with line after stock line
                rc = IgnoreTDAOptionLines(symbol, lines, ref line_index);
                if (!rc)
                    return false;
            }
        }

        if (ONE.brokerPositions.Count == 0)
        {
            Console.WriteLine($"\n***Error*** No positions related to {ONE.master_symbol} in TDA file {full_filename}");
            return false;
        }

        ONE.DisplayBrokerPositions(ONE.brokerPositions);

        ONE.DisplayIrrelevantBrokerPositions();

        return true;
    }

    // returns 0 if valid option, -1 if invalid, +1 if line does not start with "100 " (not an option line)
    //100 21 JAN 22 4795 PUT,+1,22,63.50,63.20,N/A,($30.00),($30.00),
    //100 (Quarterlys) 31 MAR 22 4795 CALL,+2,92,141.30,140.25,-4.85,($210.00),($210.00),
    //100 (Weeklys) 31 MAY 22 4650 PUT,-3,153,176.10,179.90,N/A,"($1,140.00)","($1,140.00)",
    static internal int ParseTDAOptionLine(int line_index, string line, ref Position tdaPosition)
    {
        bool rc = ONE.ParseCSVLine(line, out List<string> fields);
        if (!rc) return -1;

        string quantity_str = fields[ONE.broker_quantity_col];
        if (quantity_str.Length == 0)
            quantity_str = "0";
        rc = int.TryParse(quantity_str, out int quantity);
        if (!rc)
        {
            Console.WriteLine($"\n***Error*** In TDA file, line {line_index + 1} has an invalid option quantity: {quantity_str}.");
            return -1;
        }
        tdaPosition.Quantity = quantity;

        string option_spec = fields[ONE.broker_description_col];
        if (!option_spec.StartsWith("100 "))
            return 1; // not an option...must be stock or futures

        string[] option_fields = option_spec.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (option_fields.Length != 6 && option_fields.Length != 7)
        {
            Console.WriteLine($"\n***Error*** In TDA file, line {line_index + 1} has an invalid option specification: {option_spec}.");
            return -1;
        }

        string option_type_str = option_fields[^1].ToUpper();
        switch (option_type_str)
        {
            case "PUT":
                tdaPosition.Type = SecurityType.Put;
                break;
            case "CALL":
                tdaPosition.Type = SecurityType.Call;
                break;
            default:
                Console.WriteLine($"\n***Error*** In TDA file, line {line_index + 1} has an invalid option specification: {option_spec}.");
                return -1;
        }

        string option_strike_str = option_fields[^2];
        rc = int.TryParse(option_strike_str, out int strike);
        if (!rc)
        {
            Console.WriteLine($"\n***Error*** In TDA file, line {line_index + 1} has an invalid option specification: {option_spec}.");
            return -1;
        }
        tdaPosition.Strike = strike;

        string date_str = option_fields[^5] + ' ' + option_fields[^4] + ' ' + option_fields[^3];
        rc = DateOnly.TryParseExact(date_str, "dd MMM yy", out DateOnly expiration);
        if (!rc)
        {
            Console.WriteLine($"\n***Error*** In TDA file, line {line_index + 1} has an invalid option specification: {option_spec}.");
            return -1;
        }
        tdaPosition.Expiration = expiration;

        // check if weekly or quarterly
        if (option_fields.Length == 7)
        {
            switch (option_fields[1])
            {
                case "(Weeklys)":
                    tdaPosition.Symbol += 'W';
                    break;
                case "(Quarterlys)":
                    tdaPosition.Symbol += 'W'; // treat Quarterlys as Weeklys, since ONE doesn't export options as Quarterlys (even though the ONE GUI shows them)
                    break;
                default:
                    Console.WriteLine($"\n***Error*** In TDA file, line {line_index + 1} has an invalid option specification: {option_spec}.");
                    return -1;
            }
        }

        return 0;
    }

    // returns all lines up to second blank line; set line_index to header line
    static List<string> ReadAllRelevantLines(string filename, out int line_index)
    {
        const string headerStart = "Instrument,Qty";

        List<string> lines = new();
        using var reader = new StreamReader(filename);
        bool headerFound = false;
        line_index = 0;
        while (true)
        {
            string? line = reader.ReadLine();

            // check for end of file
            if (line == null)
                break;

            // check for header
            if (!headerFound) {
                if (line.StartsWith(headerStart))
                    headerFound = true;
                else
                    line_index++;
            }

            // break if blank line after header
            line = line.Trim();
            if (headerFound && line.Length == 0)
                break; ;

            lines.Add(line); // this is relevant line
        }

        return lines;
    }

#if false
    ///MES,+1,,4777.50,4776.25,-2.25,($6.25),($6.25),"($1,265.00)"
    //"Micro E-mini S&P 500, Mar-22 (prev. /MESH2)",+1,79,4777.50,4776.25,-2.25,($6.25),($6.25),
    static bool ParseTDAFuturesSpec(string field, string regex, out string symbol, out DateOnly expiration)
    {
        symbol = "";
        expiration = new();

        MatchCollection mc = Regex.Matches(field, regex);
        if (mc.Count > 1)
            return false;
        Match match0 = mc[0];
        if (match0.Groups.Count != 3)
            return false;
        symbol = match0.Groups[1].Value;
        string expiration_string = match0.Groups[2].Value;
        bool rc = DateOnly.TryParse(expiration_string, out expiration); // day of expiration will be incorrect (it will be 1)
        return rc;
    }
#endif

    internal static bool IgnoreTDAOptionLines(string symbol, List<string> lines, ref int line_index)
    {
        // ignore option lines until we get non-option line or end of data
        while (line_index < lines.Count)
        {
            string line = lines[line_index];
            Position tdaPosition = new(false) { Symbol = symbol };
            int rc = ParseTDAOptionLine(line_index, line, ref tdaPosition);
            if (rc < 0)
                return false;
            if (rc > 0)
                break;
            bool rcb = tdaPosition.Add(line_index, ONE.irrelevantBrokerPositions);
            if (!rcb)
                return false;

            line_index++;
        }

        return true;
    }
}

