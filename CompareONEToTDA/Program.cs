﻿using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CompareONEToTDA;

// todo: rename OptionType to SecurityType
public enum SecurityType
{
    Put,
    Call,
    Stock,
    Futures,
    None
}

enum TradeStatus
{
    Open,
    Closed
}

//,Account,Expiration,TradeId,TradeName,Underlying,Status,TradeType,OpenDate,CloseDate,DaysToExpiration,DaysInTrade,Margin,Comms,PnL,PnLperc
//,"TDA1",12/3/2021,285,"244+1lp 2021-10-11 11:37", SPX, Open, Custom,10/11/2021 11:37 AM,,53,58,158973.30,46.46,13780.74,8.67
class ONETrade
{
    public string account = "";
    public DateOnly expiration;
    public string trade_id = "";
    public string trade_name = "";
    public TradeStatus status;
    public DateTime open_dt;
    public DateTime close_dt;
    public int dte;
    public int dit;
    //public float total_commission;
    //public float pnl;

    // these are consolidated positions for trade: key is (symbol, OptionType, Expiration, Strike); value is quantity
    // so Dictionary contains no keys with quantity == 0
    public SortedDictionary<OptionKey, int> positions = new();
}

//,,Account,TradeId,Date,Transaction,Qty,Symbol,Expiry,Type,Description,Underlying,Price,Commission
//,,"TDA1",285,10/11/2021 11:37:32 AM,Buy,2,SPX   220319P04025000,3/18/2022,Put,SPX Mar22 4025 Put,SPX,113.92,2.28
class ONEPosition
{
    public string account = "";
    public string trade_id = "";
    public SecurityType optionType;
    public DateTime open_dt;
    public string symbol = ""; // SPX, SPXW, etc
    public int strike;
    public DateOnly expiration;
    public int quantity; // positive==buy, negative==sell
    public float open_price;
}

// used to sort/compare entries in consolidatedOnePositions SortedDictionary
// todo: this really isn't just an option key; it can be a stock/futures key. Should rename to SecurityKey
public class OptionKey : IComparable<OptionKey>
{
    public string Symbol { get; set; }
    public SecurityType OptionType { get; set; }
    public DateOnly Expiration { get; set; }
    public int Strike { get; set; }

    public OptionKey(string symbol, SecurityType optionType, DateOnly expiration, int strike)
    {
        this.Symbol = symbol;
        this.OptionType = optionType;
        this.Expiration = expiration;
        this.Strike = strike;
    }

    public override int GetHashCode()
    {
        return Symbol.GetHashCode() ^ OptionType.GetHashCode() ^ Expiration.GetHashCode() ^ Strike.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj is OptionKey other)
        {
            return other != null && Symbol == other.Symbol && OptionType == other.OptionType && Expiration == other.Expiration && Strike == other.Strike;
        }
        return false;
    }

    public int CompareTo(OptionKey? other)
    {
        Debug.Assert(other != null);
        if (other == null)
            return 1;

        bool thisIsOption = OptionType == SecurityType.Put || OptionType == SecurityType.Call;
        bool otherIsOption = other.OptionType == SecurityType.Put || other.OptionType == SecurityType.Call;
        if (!thisIsOption)
        {
            // this is stock/future

            if (otherIsOption)
                return -1; // this is stock/future, other is option: stocks/futures come before options

            // this and other are both Stocks/Futures: stocks come before futures, then symbol, then, if future, expiration

            if (OptionType == SecurityType.Stock)
            {
                if (other.OptionType == SecurityType.Futures)
                    return -1; // stocks come before futures

                // this and other are both stocks...sort by symbol
                return Symbol.CompareTo(other.Symbol);
            }

            // this is futures

            if (other.OptionType == SecurityType.Stock)
                return 1; // this is futures, other is stock: futures come after stocks

            // this and other are both futures..sort by symbol then expiration
            if (Symbol != other.Symbol)
                return Symbol.CompareTo(other.Symbol);

            return Expiration.CompareTo(other.Expiration);
        }

        // this is an option

        if (!otherIsOption)
            return 1; // other is stock/future; stocks/futures come before options

        // this and other are both options; sort by expiration, then strike, then symbol (like SPX, SPXW), finally type (put/Call)
#if true
        if (other.Expiration != this.Expiration)
            return Expiration.CompareTo(other.Expiration);
        else if (other.Strike != Strike)
            return Strike.CompareTo(other.Strike);
        else if (other.Symbol != Symbol)
            return other.Symbol.CompareTo(Symbol);
        else // this 
            return OptionType.CompareTo(other.OptionType);
#else
        if (other.Symbol != this.Symbol)
            return Symbol.CompareTo(other.Symbol);
        else if (other.Strike != Strike)
            return Strike.CompareTo(other.Strike);
        else if (other.Expiration != Expiration)
            return other.Expiration.CompareTo(Expiration);
        else // this 
            return OptionType.CompareTo(other.OptionType);
#endif
    }
}

// used to sort/compare entries in various Dictionaries that hold option, futures, stock positions
public class Position : IComparable<Position>
{
    public string Symbol { get; set; }
    public SecurityType Type { get; set; }
    public DateOnly Expiration { get; set; } = new();
    public int Strike { get; set; } = 0;
    public int Quantity { get; set; } = 0;

    public Position(string symbol, SecurityType type)
    {
        this.Symbol = symbol;
        this.Type = type;
    }

    public Position(string symbol, SecurityType type, DateOnly expiration, int strike)
    {
        this.Symbol = symbol;
        this.Type = type;
        this.Expiration = expiration;
        this.Strike = strike;
    }

    public override int GetHashCode()
    {
        return Symbol.GetHashCode() ^ Type.GetHashCode() ^ Expiration.GetHashCode() ^ Strike.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj is Position other)
        {
            return other != null && Symbol == other.Symbol && Type == other.Type && Expiration == other.Expiration && Strike == other.Strike;
        }
        return false;
    }

    public int CompareTo(Position? other)
    {
        Debug.Assert(other != null);
        if (other == null)
            return 1;

        bool thisIsOption = Type == SecurityType.Put || Type == SecurityType.Call;
        bool otherIsOption = other.Type == SecurityType.Put || other.Type == SecurityType.Call;
        if (!thisIsOption)
        {
            // this is stock/future

            if (otherIsOption)
                return -1; // this is stock/future, other is option: stocks/futures come before options

            // this and other are both Stocks/Futures: stocks come before futures, then symbol, then, if future, expiration

            if (Type == SecurityType.Stock)
            {
                if (other.Type == SecurityType.Futures)
                    return -1; // stocks come before futures

                // this and other are both stocks...sort by symbol
                return Symbol.CompareTo(other.Symbol);
            }

            // this is futures

            if (other.Type == SecurityType.Stock)
                return 1; // this is futures, other is stock: futures come after stocks

            // this and other are both futures..sort by symbol then expiration
            if (Symbol != other.Symbol)
                return Symbol.CompareTo(other.Symbol);

            return Expiration.CompareTo(other.Expiration);
        }

        // this is an option

        if (!otherIsOption)
            return 1; // other is stock/future; stocks/futures come before options

        // this and other are both options; sort by expiration, then strike, then symbol (like SPX, SPXW), finally type (put/Call)
#if true
        if (other.Expiration != this.Expiration)
            return Expiration.CompareTo(other.Expiration);
        else if (other.Strike != Strike)
            return Strike.CompareTo(other.Strike);
        else if (other.Symbol != Symbol)
            return other.Symbol.CompareTo(Symbol);
        else // this 
            return Type.CompareTo(other.Type);
#else
        if (other.Symbol != this.Symbol)
            return Symbol.CompareTo(other.Symbol);
        else if (other.Strike != Strike)
            return Strike.CompareTo(other.Strike);
        else if (other.Expiration != Expiration)
            return other.Expiration.CompareTo(Expiration);
        else // this 
            return OptionType.CompareTo(other.OptionType);
#endif
    }
}

class TDAPosition : Position
{
    // used only during reconciliation with ONE positions
    public int one_quantity = 0;
    public HashSet<string> oneTrades = new();

    internal TDAPosition(string symbol) : base(symbol, SecurityType.None)
    {
    }

    internal TDAPosition(string symbol, SecurityType type) : base(symbol, type)
    {
    }

    internal TDAPosition(string symbol, SecurityType type, int quantity) : base(symbol, type)
    {
        Quantity = quantity;
    }

    internal TDAPosition(OptionKey optionKey) : base(optionKey.Symbol, optionKey.OptionType, optionKey.Expiration, optionKey.Strike)
    {
    }

    // error (display message, return false) if position already exists
    // programming error if this.Quantity is 0
    internal bool Add(int line_index, HashSet<TDAPosition> tdaPositions)
    {
        Debug.Assert(this.Quantity != 0);
        if (tdaPositions.Contains(this))
        {
            switch (Type)
            {
                case SecurityType.None:
                    Console.WriteLine($"***Error*** in TDA line {line_index}: duplicate position: {Symbol} {Type} {Expiration} {Strike}");
                    break;
                case SecurityType.Futures:
                    Console.WriteLine($"***Error*** in TDA line {line_index}: duplicate futures contract: {Symbol} {Expiration} ");
                    break;
                default:
                    Console.WriteLine($"***Error*** in TDA line {line_index}: duplicate option: {Symbol} {Type} {Expiration} {Strike}");
                    break;
            }
            return false;
        }
        tdaPositions.Add(this);
        return true;
    }

    // in this function, it's ok for the position to already exist...just acumulate quantity
    // return true if resulting quantity is 0; return false if resulting quantity is 0
    // if resulting quantity is 0, position is removed from collection (or not added)
    internal bool Accumulate(HashSet<TDAPosition> tdaPositions)
    {
        tdaPositions.TryGetValue(this, out TDAPosition? existingPosition);
        if (existingPosition != null)
        {
            existingPosition.Quantity += this.Quantity;
            if (existingPosition.Quantity == 0)
            {
                tdaPositions.Remove(this);
                return false;
            }
            return true;
        }
        if (this.Quantity == 0)
            return false;
        tdaPositions.Add(this);
        return true;
    }
}

static class Program
{
    internal const string version = "0.0.3";
    internal const string version_date = "2022-01-06";
    internal static string? tda_filename = null;
    internal static string tda_directory = "";
    internal static string? one_filename = null;
    internal static string one_directory = "";
    internal static string master_symbol = "SPX";
    internal static string one_account = "";
    internal static int index_of_last_required_column = -1;
    internal static int tda_symbol_col = -1;
    internal static int tda_quantity_col = -1;

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

    // ONE uses the main index symbol for positions in the underlying, whereas TDA uses an actual stock/futures symbol
    // so...to reconcile these, if ONE has a position, say, of 10 SPX, this could be equivalent to 2 MES contracts,
    // 100 SPY shares, or some combination, like 1 MES contract and 50 SPY shares
    // So...the float is the number of ONE SPX shares that a share of the given item represents. So, { "SPY", 0.1f }
    // means that 1 share of SPY in TDA represents 0.1 shares of SPX in ONE
    readonly internal static Dictionary<string, Dictionary<string, float>> associated_symbols = new()
    {
        { "SPX", new Dictionary<string, float> { { "SPY", 0.1f }, { "MES", 5f }, { "ES", 50f } } },
        { "RUT", new Dictionary<string, float> { { "IWM", 0.1f }, { "M2K", 5f }, { "RTY", 50f } } },
        { "NDX", new Dictionary<string, float> { { "QQQ", 0.1f }, { "MNQ", 5f }, { "NQ", 50f } } }
    };
    internal static Dictionary<string, float> relevant_symbols = new(); // set to: associated_symbols[master_symbol];

    // note: the ref is readonly, not the contents of the Dictionary
    static readonly Dictionary<string, int> tda_columns = new(); // key is column name, value is column index
    static readonly Dictionary<string, int> one_trade_columns = new(); // key is column name, value is column index
    static readonly Dictionary<string, int> one_position_columns = new(); // key is column name, value is column index
    static readonly Dictionary<string, ONETrade> oneTrades = new(); // key is trade_id

    static readonly HashSet<TDAPosition> tdaPositions = new();

    // these TDA positions are not relevant to specified master_symbol, but we want to display them so user can verify
    static readonly HashSet<TDAPosition> irrelevantTDAPositions = new();

    // dictionary of ONE trades with key of trade id
    static readonly SortedDictionary<string, ONETrade> ONE_trades = new();

    // consolidated ONE positions; key is (symbol, OptionType, Expiration, Strike); value is (quantity, HashSet<string>); string is trade id 
    static readonly SortedDictionary<OptionKey, (int, HashSet<string>)> consolidatedOnePositions = new();

    static void RunUnitTests()
    {
        // tests for ParseCSVLine
        bool ParseCSVLineRC = ParseCSVLine(-1, 0, ",", out List<string> parseCSVTestFields);
        Debug.Assert(ParseCSVLineRC && parseCSVTestFields.Count == 1 && parseCSVTestFields[0] == "");
        parseCSVTestFields.Clear();
        ParseCSVLineRC = ParseCSVLine(-1, 0, ",,", out parseCSVTestFields); // trailing comma ignored
        Debug.Assert(ParseCSVLineRC && parseCSVTestFields.Count == 2 && parseCSVTestFields[0] == "" && parseCSVTestFields[1] == "");
        parseCSVTestFields.Clear();
        ParseCSVLineRC = ParseCSVLine(-1, 0, "1,", out parseCSVTestFields); // trailing comma ignored
        Debug.Assert(ParseCSVLineRC && parseCSVTestFields.Count == 1 && parseCSVTestFields[0] == "1");
        ParseCSVLineRC = ParseCSVLine(-1, 1, "1,2", out parseCSVTestFields); // trailing comma ignored
        Debug.Assert(ParseCSVLineRC && parseCSVTestFields.Count == 2 && parseCSVTestFields[1] == "2");
        //int test1 = 1;
    }

    static int Main(string[] args)
    {
#if true
        RunUnitTests();
#endif

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        CommandLine.ProcessCommandLineArguments(args); // calls System.Environment.Exit(-1) if bad command line arguments
        master_symbol = master_symbol.ToUpper();
        relevant_symbols = associated_symbols[master_symbol];
        Console.WriteLine($"CompareONEToTDA Version {version}, {version_date}. Processing trades for {master_symbol}");

        if (one_filename == null)
            one_filename = GetONEFileName();
        if (tda_filename == null)
            tda_filename = GetTDAFileName();
        if (one_filename == null || tda_filename == null)
            return -1;

        Console.WriteLine("\nProcessing ONE file: " + one_filename);
        Console.WriteLine("Processing TDA file: " + tda_filename);

        bool rc = ProcessONEFile(one_filename);
        if (!rc)
            return -1;

        // display ONE positions
        DisplayONEPositions();

        rc = ProcessTDAFile(tda_filename);
        if (!rc)
            return -1;

        // display TDA positions
        DisplayTDAPositions();

        if (irrelevantTDAPositions.Count > 0)
            DisplayIrrelevantTDAPositions();

        rc = CompareONEPositionsToTDAPositions();
        if (!rc)
            return -1;

        Console.WriteLine($"\nSuccess: TDA and ONE positions for {master_symbol} are equivalent.");

        stopWatch.Stop();
        Console.WriteLine($"\nElapsed time = {stopWatch.Elapsed}");

        return 0;
    }

    static string? GetONEFileName()
    {
        Debug.Assert(Directory.Exists(one_directory));

        const string ending = "-ONEDetailReport.csv";
        string[] files;

        DateTime latestDate = new(1000, 1, 1);
        string latest_full_filename = "";
        files = Directory.GetFiles(one_directory, '*' + ending, SearchOption.TopDirectoryOnly);
        bool file_found = false;
        foreach (string full_filename in files)
        {
            string filename = Path.GetFileName(full_filename);
            string datestr = filename[..^ending.Length];
            if (DateTime.TryParse(datestr, out DateTime dt))
            {
                file_found = true;
                if (dt > latestDate)
                {
                    latestDate = dt;
                    latest_full_filename = full_filename;
                }
            }
        }

        if (!file_found)
        {
            Console.WriteLine($"\n***Error*** No ONE files found in {one_directory} with following filename pattern: yyyy-mm-dd-ONEDetailReport.csv");
            return null;
        }

        return latest_full_filename;
    }

    static string? GetTDAFileName()
    {
        Debug.Assert(Directory.Exists(tda_directory));

        const string filename_pattern = "????-??-??-PositionStatement.csv"; // file names look like: yyyy-mm-dd-PositionStatement.csv

        string[] files;
        DateOnly latestDate = new(1000, 1, 1);
        string latest_full_filename = "";

        files = Directory.GetFiles(tda_directory, filename_pattern, SearchOption.TopDirectoryOnly);
        bool file_found = false;
        foreach (string full_filename in files)
        {
            string filename = Path.GetFileName(full_filename);
            if (filename.Length < filename_pattern.Length)
                continue;
            string datestr = filename[..10]; // yyyy-mm-dd
            if (!int.TryParse(datestr[..4], out int year))
                continue;
            if (!int.TryParse(datestr.AsSpan(5, 2), out int month))
                continue;
            if (!int.TryParse(datestr.AsSpan(8, 2), out int day))
                continue;

            file_found = true;
            DateOnly dt = new(year, month, day);
            if (dt > latestDate)
            {
                latestDate = dt;
                latest_full_filename = full_filename;
            }
        }

        if (!file_found)
        {
            Console.WriteLine($"\n***Error*** No TDA Position files found in {tda_directory} with following filename pattern: yyyy-mm--ddPositionStatement.csv");
            return null;
        }

        return latest_full_filename;
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
    static internal bool ProcessTDAFile(string full_filename)
    {
        List<string> lines = ReadAllRelevantLines(full_filename); // throws away all lines from "Cash & Sweep Vehicle" to end       
        if (lines.Count < 5)
        {
            Console.WriteLine("\n***Error*** TDA File must contain at least 5 non-blank lines");
            return false;
        }

        int line_index = 0;
        string line = lines[line_index++];
        if (!line.StartsWith("Position Statement for"))
        {
            Console.WriteLine("\n***Error*** TDA file must begin with line containing the phrase 'Position Statement for...'");
            return false;
        }

        line = lines[line_index++];
        if (line != "None")
        {
            Console.WriteLine("***\nError*** In TDA file, line containing the word 'None' must follow line conatining phrase 'Position Statement for...'");
            return false;
        }

        // check for required columns and get index of last required column
        string[] required_columns = { "Instrument", "Qty" };
        line = lines[line_index++];
        string[] column_names = line.Split(',');
        for (int i = 0; i < column_names.Length; i++)
        {
            string column_name = column_names[i].Trim();
            if (column_name.Length > 0)
                tda_columns.Add(column_name, i);
        }
        for (int i = 0; i < required_columns.Length; i++)
        {
            if (!tda_columns.TryGetValue(required_columns[i], out int colnum))
            {
                Console.WriteLine($"\n***Error*** TDA file header must contain column named {required_columns[i]}");
                return false;
            }
            index_of_last_required_column = Math.Max(colnum, index_of_last_required_column);
        }
        tda_symbol_col = tda_columns["Instrument"];
        tda_quantity_col = tda_columns["Qty"];

        // now process each TDA position (stock, index, or futures), each consisting of 2 lines plus option lines starting with "100 "
        while (line_index < lines.Count)
        {
            line = lines[line_index];
            bool rc = ParseCSVLine(line_index++, index_of_last_required_column, line, out List<string> fields);
            if (!rc) return false;

            if (fields.Count < index_of_last_required_column + 1)
            {
                Console.WriteLine($"\n***Error*** In TDA file, at line {line_index}, first line of position must have {index_of_last_required_column + 1} fields, not {fields.Count}");
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
            string symbol = fields[tda_symbol_col];

            string line2 = lines[line_index];
            rc = ParseCSVLine(line_index++, index_of_last_required_column, line2, out List<string> fields2);
            if (!rc) return false;

            if (fields2.Count < index_of_last_required_column + 1)
            {
                Console.WriteLine($"\n***Error*** In TDA file, at line {line_index}, second line of position must have {index_of_last_required_column + 1} fields, not {fields2.Count}");
                return false;
            }
            string symbol2 = fields2[tda_symbol_col];

            // get quantity from second line because for index and stocks, quantity in first line is 0 (quantity for index is always 0 in both lines)
            rc = int.TryParse(fields2[tda_quantity_col], out int quantity);
            if (!rc)
            {
                Console.WriteLine($"***Error*** In TDA file, in line {line_index}: invalid Quantity: {fields2[tda_quantity_col]}");
                return false;
            }

            if (symbol == master_symbol)
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
                    line = lines[line_index]; // use line here because this line might be first line of next position
                    TDAPosition tdaPosition = new(symbol);
                    int irc = ParseTDAOptionLine(line_index, line, ref tdaPosition); // fills in securityType, expiration, and strike
                    if (irc < 0)
                        return false; // line has invalid syntax; ParseTDAOptionLine() has already display error message
                    if (irc > 0)
                        break; // line isn't an option position (doesn't start with "100 "; must be start of next position

                    // add new option to tdaPositions collection
                    rc = tdaPosition.Add(line_index, tdaPositions);
                    if (!rc)
                        return false;
                    num_options++;
                    line_index++;
                }

                if (num_options == 0)
                {
                    Console.WriteLine($"\n***Error*** In TDA file, line {line_index - 2} specifies {master_symbol} index, but no option position lines follow (lines that start with \"100 \").");
                    return false;
                }
            }
            else if (symbol.StartsWith('/'))
            {
                // This is futures symbol: it is only relevant if it is one of the futures associated with index we are checking;
                // All options on this future are ignored
                ///MES,+1,,4777.50,4776.25,-2.25,($6.25),($6.25),"($1,265.00)"
                //"Micro E-mini S&P 500, Mar-22 (prev. /MESH2)",+1,79,4777.50,4776.25,-2.25,($6.25),($6.25),
                string futures_root = symbol[1..];
                TDAPosition tdaPosition = new(futures_root, SecurityType.Futures, quantity);
                if (associated_symbols[master_symbol].ContainsKey(futures_root))
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
                    tdaPosition.Add(line_index, tdaPositions);
                }
                else
                    tdaPosition.Add(line_index, irrelevantTDAPositions);

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
                TDAPosition tdaPosition = new(symbol, SecurityType.Stock);
                tdaPosition.Quantity = quantity;
                if (associated_symbols[master_symbol].ContainsKey(symbol))
                    tdaPosition.Add(line_index, tdaPositions);
                else
                    tdaPosition.Add(line_index, irrelevantTDAPositions);

                rc = IgnoreTDAOptionLines(symbol, lines, ref line_index);
                if (!rc)
                    return false;
            }
        }

        if (tdaPositions.Count == 0)
        {
            Console.WriteLine($"\n***Error*** No positions related to {master_symbol} in TDA file {full_filename}");
            return false;
        }

        return true;
    }

    // returns 0 if valid option, -1 if invalid, +1 if line does not start with "100 " (not n option line)
    //100 21 JAN 22 4795 PUT,+1,22,63.50,63.20,N/A,($30.00),($30.00),
    //100 (Quarterlys) 31 MAR 22 4795 CALL,+2,92,141.30,140.25,-4.85,($210.00),($210.00),
    //100 (Weeklys) 31 MAY 22 4650 PUT,-3,153,176.10,179.90,N/A,"($1,140.00)","($1,140.00)",
    static internal int ParseTDAOptionLine(int line_index, string line, ref TDAPosition tdaPosition)
    {
        bool rc = ParseCSVLine(line_index, index_of_last_required_column, line, out List<string> fields);
        if (!rc) return -1;

        string quantity_str = fields[tda_quantity_col];
        if (quantity_str.Length == 0)
            quantity_str = "0";
        rc = int.TryParse(quantity_str, out int quantity);
        if (!rc)
        {
            Console.WriteLine($"\n***Error*** In TDA file, line {line_index + 1} has an invalid option quantity: {quantity_str}.");
            return -1;
        }
        tdaPosition.Quantity = quantity;

        string option_spec = fields[tda_symbol_col];
        if (!option_spec.StartsWith("100 "))
            return 1;

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

    static List<string> ReadAllRelevantLines(string filename)
    {
        // if the file represents a paper trading file, the file start with an extra line:
        const string simulated_position_header = "This document was exported from the paperMoney® platform";

        List<string> lines = new();
        using var reader = new StreamReader(filename);
        while (true)
        {
            string? line = reader.ReadLine();

            // check for end of file
            if (line == null)
                break;

            // ignore blank lines
            line = line.Trim();
            if (line.Length == 0)
                continue;

            // check for final line
            if (line.StartsWith("Cash & Sweep Vehicle"))
                break;

            // check for simulated position line
            if (lines.Count == 0 && line.StartsWith(simulated_position_header))
                continue;

            lines.Add(line); // this is relevant line
        }

        return lines;
    }

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

    internal static bool IgnoreTDAOptionLines(string symbol, List<string> lines, ref int line_index)
    {
        // ignore option lines until we get non-option line or end of data
        while (line_index < lines.Count)
        {
            string line = lines[line_index];
            TDAPosition tdaPosition = new(symbol);
            int rc = ParseTDAOptionLine(line_index, line, ref tdaPosition);
            if (rc < 0)
                return false;
            if (rc > 0)
                break;
            bool rcb = tdaPosition.Add(line_index, irrelevantTDAPositions);
            if (!rcb)
                return false;

            line_index++;
        }

        return true;
    }

    //ONE Detail Report
    //
    //Date/Time: 12/8/2021 08:28:42
    //Filter: [Account] = 'TDA1'
    //Grouping: Account
    //
    //,Account,Expiration,TradeId,TradeName,Underlying,Status,TradeType,OpenDate,CloseDate,DaysToExpiration,DaysInTrade,Margin,Comms,PnL,PnLperc
    //,,Account,TradeId,Date,Transaction,Qty,Symbol,Expiry,Type,Description,Underlying,Price,Commission
    //TDA1
    //,"TD1",12/3/2021,285,"244+1lp 2021-10-11 11:37", SPX, Open, Custom,10/11/2021 11:37 AM,,53,58,158973.30,46.46,13780.74,8.67
    //,,"TDA1",285,10/11/2021 11:37:32 AM,Buy,2,SPX   220319P04025000,3/18/2022,Put,SPX Mar22 4025 Put,SPX,113.92,2.28
    //,,"TDA1",285,10/11/2021 11:37:32 AM,Buy,4,SPX   220319P02725000,3/18/2022,Put,SPX Mar22 2725 Put,SPX,12.8,4.56
    //,,"TDA1",296,11/12/2021 11:02:02 AM,Buy,1,SPX,,Stock,SPX Stock, SPX,4660.05,0.005
    static bool ProcessONEFile(string full_filename)
    {
        string[] lines = File.ReadAllLines(full_filename);
        if (lines.Length < 9)
        {
            Console.WriteLine("\n***Error*** ONE File must contain at least 9 lines");
            return false;
        }

        string? line1 = lines[0].Trim();
        if (line1 != "ONE Detail Report")
        {
            Console.WriteLine($"\n***Error*** First line of ONE file must be 'ONE Detail Report', not: {line1}");
            return false;
        }

        line1 = lines[1].Trim();
        if (line1.Length != 0)
        {
            Console.WriteLine($"\n***Error*** Second line of ONE must be blank, not: {line1}");
            return false;
        }

        line1 = lines[2].Trim();
        if (!line1.StartsWith("Date/Time:"))
        {
            Console.WriteLine($"\n***Error*** Third line of ONE file must start with 'Date/Time:', not: {line1}");
            return false;
        }

        line1 = lines[3].Trim();
        if (!line1.StartsWith("Filter: [Account]"))
        {
            Console.WriteLine($"\n***Error*** Fourth line of ONE file must start with 'Filter: [Account]', not: {line1}");
            return false;
        }

        line1 = lines[4].Trim();
        if (!line1.StartsWith("Grouping: Account"))
        {
            Console.WriteLine($"\n***Error*** Fifth line of ONE file must start with 'Grouping: Account', not: {line1}");
            return false;
        }

        line1 = lines[5].Trim();
        if (line1.Length != 0)
        {
            Console.WriteLine($"\n***Error*** Sixth line of ONE must be blank, not: {line1}");
            return false;
        }

        // check for required trade line columns (in trade header) and get index of last required trade line column
        string[] trade_required_columns = { "Account", "Expiration", "TradeId", "Underlying", "Status", "OpenDate", "CloseDate", "DaysToExpiration", "DaysInTrade" };
        line1 = lines[6].Trim();
        string[] trade_column_names = line1.Split(',');
        for (int i = 0; i < trade_column_names.Length; i++)
        {
            string column_name = trade_column_names[i].Trim();
            if (column_name.Length > 0)
                one_trade_columns.Add(column_name, i);
        }
        int index_of_last_required_trade_column = 0;
        for (int i = 0; i < trade_required_columns.Length; i++)
        {
            if (!one_trade_columns.TryGetValue(trade_required_columns[i], out int colnum))
            {
                Console.WriteLine($"\n***Error*** ONE trade line header must contain column named {trade_required_columns[i]}");
                return false;
            }
            index_of_last_required_trade_column = Math.Max(colnum, index_of_last_required_trade_column);
        }

        // check for required position line columns (in position header) and get index of last required position line column
        string[] position_required_columns = { "Account", "TradeId", "Date", "Transaction", "Qty", "Symbol", "Expiry", "Type", "Description", "Underlying" };
        line1 = lines[7].Trim();
        string[] position_column_names = line1.Split(',');
        for (int i = 0; i < position_column_names.Length; i++)
        {
            string column_name = position_column_names[i].Trim();
            if (column_name.Length > 0)
                one_position_columns.Add(column_name, i);
        }
        int index_of_last_required_position_column = 0;
        for (int i = 0; i < position_required_columns.Length; i++)
        {
            if (!one_position_columns.TryGetValue(position_required_columns[i], out int colnum))
            {
                Console.WriteLine($"\n***Error*** ONE position line header must contain column named {position_required_columns[i]}");
                return false;
            }
            index_of_last_required_position_column = Math.Max(colnum, index_of_last_required_position_column);
        }

        // account appears here in line 9, in the Trade lines, and in the Position lines. They must all match, but one_account is set here
        one_account = lines[8].Trim();
        if (one_account.Length == 0)
        {
            Console.WriteLine($"\n***Error*** Ninth line of ONE must be ONE account name, not blank");
            return false;
        }

        // parse Trade and Position lines
        ONETrade? curOneTrade = null;
        for (int line_index = 9; line_index < lines.Length; line_index++)
        {
            string line = lines[line_index].Trim();

            // trades (except for the first one) are separated by blanks
            if (line.Length == 0)
            {
                curOneTrade = null;
                continue;
            }
            bool rc = ParseCSVLine(line_index, index_of_last_required_column, line, out List<string> fields);
            if (!rc)
                return false;
            // fields[0] must be blank; but, I don't check here yet

            string account1 = fields[1].Trim();
            if (account1.Length != 0)
            {
                // this is trade line
                if (curOneTrade != null)
                {
                    // do whatever final stuff we need to do when we've parsed all position lines for trade
                }

                if (fields.Count < index_of_last_required_trade_column + 1)
                {
                    Console.WriteLine($"\n***Error*** ONE Trade line {line_index + 1} must have at least {index_of_last_required_trade_column + 1} fields, not {fields.Count} fields");
                    return false;
                }

                // start new trade - save it in trades Dictionary
                curOneTrade = ParseONETradeLine(line_index, fields);
                if (curOneTrade == null)
                    return false;

                ONE_trades.Add(curOneTrade.trade_id, curOneTrade);
                continue;
            }

            // this is position line
            if (curOneTrade == null)
            {
                Console.WriteLine($"\n***Error*** ONE Position line {line_index + 1} comes before Trade line.");
                return false;
            }

            if (fields.Count < index_of_last_required_position_column + 1)
            {
                Console.WriteLine($"\n***Error*** ONE Trade line {line_index + 1} must have at least {index_of_last_required_position_column + 1} fields, not {fields.Count} fields");
                return false;
            }

            ONEPosition? position = ParseONEPositionLine(line_index, fields, curOneTrade.trade_id);
            if (position == null)
                return false;

            // now add position to trade's positions dictionary; remove existing position if quantity now 0
            var key = new OptionKey(position.symbol, position.optionType, position.expiration, position.strike);

            // within trade, we consolidate individual trades to obtain an overall current position
            if (curOneTrade.positions.ContainsKey(key))
            {
                curOneTrade.positions[key] += position.quantity;

                // remove position if it now has 0 quantity
                if (curOneTrade.positions[key] == 0)
                    curOneTrade.positions.Remove(key);
            }
            else
            {
                Debug.Assert(position.quantity != 0); // todo: should be error message
                curOneTrade.positions.Add(key, position.quantity);
            }
        }

        if (ONE_trades.Count == 0)
        {
            Console.WriteLine($"\n***Error*** No trades in ONE file {full_filename}");
            return false;
        }

        DisplayONETrades();

        RemoveClosedONETrades();

        CreateConsolidateOnePositions();

        return true;
    }

    // go through each ONE trade in ONE_trades and create a consolidated ONE position
    // right now, this could create ONE positions with 0 quantity if trades "step on strikes"
    static void CreateConsolidateOnePositions()
    {
        foreach (ONETrade one_trade in ONE_trades.Values)
        {
            foreach ((OptionKey key, int quantity) in one_trade.positions)
            {
                Debug.Assert(quantity != 0);
                if (!consolidatedOnePositions.ContainsKey(key))
                {
                    HashSet<string> trade_ids = new();
                    trade_ids.Add(one_trade.trade_id);
                    consolidatedOnePositions.Add(key, (quantity, trade_ids));
                }
                else
                {
                    int new_quantity = consolidatedOnePositions[key].Item1 + quantity;
                    HashSet<string> trade_ids = consolidatedOnePositions[key].Item2;
                    Debug.Assert(!trade_ids.Contains(one_trade.trade_id));
                    trade_ids.Add(one_trade.trade_id);
                    consolidatedOnePositions[key] = (new_quantity, trade_ids);
                }
            }
        }
    }

    // note: this also removes trades from ONE_trades which are closed
    static void DisplayONETrades()
    {
        Console.WriteLine("\nONE Trades:");
        foreach (ONETrade one_trade in ONE_trades.Values)
        {
            if (one_trade.status == TradeStatus.Closed)
            {
                if (one_trade.positions.Count != 0)
                {
                    Console.WriteLine($"\n***Error*** Trade {one_trade.trade_id} is closed, but contains positions:");
                }
                else
                {
                    Console.WriteLine($"\nTrade {one_trade.trade_id}: Closed. No positions");
                    continue;
                }
            }
            else
                Console.WriteLine($"\nTrade {one_trade.trade_id}:");

            if (one_trade.positions.Count == 0)
            {
                Debug.Assert(one_trade.status == TradeStatus.Open);
                Console.WriteLine($"***Error*** Trade Open but no net positions.");
                continue;
            }

            foreach ((OptionKey key, int quantity) in one_trade.positions)
            {
                switch (key.OptionType)
                {
                    case SecurityType.Stock:
                        Console.WriteLine($"{master_symbol}\tIndex\tquantity: {quantity}");
                        break;
                    case SecurityType.Put:
                    case SecurityType.Call:
                        Console.WriteLine($"{key.Symbol}\t{key.OptionType}\tquantity: {quantity}\texpiration: {key.Expiration}\tstrike: {key.Strike}");
                        break;
                    default:
                        Debug.Assert(false, $"Invalid key.OptionType in ONE_trades: {key.OptionType}");
                        break;
                }
            }
        }
    }

    // remove closed trades from ONE_trades
    static void RemoveClosedONETrades()
    {
        List<string> closedTradeIds = new();
        foreach (ONETrade one_trade in ONE_trades.Values)
        {
            if (one_trade.status == TradeStatus.Closed)
                closedTradeIds.Add(one_trade.trade_id);
        }
        foreach (string id in closedTradeIds)
            ONE_trades.Remove(id);
    }

    // ",Account,Expiration,TradeId,TradeName,Underlying,Status,TradeType,OpenDate,CloseDate,DaysToExpiration,DaysInTrade,Margin,Comms,PnL,PnLperc"
    //,"TDA1",12/3/2021,285,"244+1lp 2021-10-11 11:37", SPX, Open, Custom,10/11/2021 11:37 AM,,53,58,158973.30,46.46,13780.74,8.67
    // we don't parse Margin,Comms,PnL,PnLperc
    static ONETrade? ParseONETradeLine(int line_index, List<string> fields)
    {
        ONETrade oneTrade = new();

        oneTrade.account = fields[one_trade_columns["Account"]];
        if (one_account != oneTrade.account)
        {
            Console.WriteLine($"\n***Error*** In ONE Trade line #{line_index + 1}, account field: {oneTrade.account} is not the same as line 9 of file: {one_account}");
            return null;
        }

        int field_index = one_trade_columns["Expiration"];
        if (!DateOnly.TryParse(fields[field_index], out oneTrade.expiration))
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has invalid date field: {fields[field_index]}");
            return null;
        }

        oneTrade.trade_id = fields[one_trade_columns["TradeId"]];
        if (oneTrade.trade_id.Length == 0)
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has empty trade id field");
            return null;
        }

        oneTrade.trade_name = fields[one_trade_columns["TradeName"]];

        field_index = one_trade_columns["Underlying"];
        if (master_symbol != fields[field_index])
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} is for symbol other than {master_symbol}: {fields[field_index]}");
            return null;
        }

        string status = fields[one_trade_columns["Status"]];
        if (status == "Open")
            oneTrade.status = TradeStatus.Open;
        else if (status == "Closed")
            oneTrade.status = TradeStatus.Closed;
        else
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has invalid trade status field: {status}");
            return null;
        }

        string open_dt = fields[one_trade_columns["OpenDate"]];
        if (!DateTime.TryParse(open_dt, out oneTrade.open_dt))
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has invalid date field: {open_dt}");
            return null;
        }

        if (oneTrade.status == TradeStatus.Closed)
        {
            string close_dt = fields[one_trade_columns["CloseDate"]];
            if (!DateTime.TryParse(close_dt, out oneTrade.close_dt))
            {
                Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has invalid date field: {close_dt}");
                return null;
            }
        }

        string dte = fields[one_trade_columns["DaysToExpiration"]];
        if (!int.TryParse(dte, out oneTrade.dte))
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has invalid dte field: {dte}");
            return null;
        }

        string dit = fields[one_trade_columns["DaysInTrade"]];
        if (!int.TryParse(dit, out oneTrade.dit))
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has invalid dit field: {dit}");
            return null;
        }

        if (oneTrades.ContainsKey(oneTrade.trade_id))
        {
            Console.WriteLine($"\n***Error*** in #{line_index + 1} in ONE file: duplicate trade id: {oneTrade.trade_id}");
            return null;
        }
        oneTrades.Add(oneTrade.trade_id, oneTrade);

        return oneTrade;
    }

    //,,Account,TradeId,Date,Transaction,Qty,Symbol,Expiry,Type,Description,Underlying,Price,Commission
    //,,"TDA1",285,10/11/2021 11:37:32 AM,Buy,2,SPX   220319P04025000,3/18/2022,Put,SPX Mar22 4025 Put,SPX,113.92,2.28
    //,,"TDA1",294,11/1/2021 12:24:57 PM,Buy,2,SPX,,Stock,SPX Stock, SPX,4609.8,0.01
    // note there is no Futures position in ONE...a Futures position is represented as Stock
    static ONEPosition? ParseONEPositionLine(int line_index, List<string> fields, string trade_id)
    {
        string account = fields[one_position_columns["Account"]];
        if (account != one_account)
        {
            Console.WriteLine($"\n***Error*** ONE Position line #{line_index + 1} has account: {account} that is different from trade account: {one_account}");
            return null;
        }

        string tid = fields[one_position_columns["TradeId"]];
        if (tid != trade_id)
        {
            Console.WriteLine($"\n***Error*** ONE Position line #{line_index + 1} has trade id: {tid} that is different from trade id in trade line: {trade_id}");
            return null;
        }

        ONEPosition position = new();
        position.account = one_account;
        position.trade_id = trade_id;

        string open_dt = fields[one_position_columns["Date"]];
        if (!DateTime.TryParse(open_dt, out position.open_dt))
        {
            Console.WriteLine($"\n***Error*** ONE Position line #{line_index + 1} has invalid open date field: {open_dt}");
            return null;
        }

        string transaction = fields[one_position_columns["Transaction"]];
        int quantity_sign;
        switch (transaction)
        {
            case "Buy":
                quantity_sign = 1; break;
            case "Sell":
                quantity_sign = -1; break;
            default:
                Console.WriteLine($"\n***Error*** ONE Position line #{line_index + 1} has invalid transaction type (must be Buy or Sell): {transaction}");
                return null;
        }

        string qty = fields[one_position_columns["Qty"]];
        if (!int.TryParse(qty, out position.quantity))
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has invalid quantity field: {qty}");
            return null;
        }
        position.quantity *= quantity_sign;

        string type = fields[one_position_columns["Type"]];
        string symbol = fields[one_position_columns["Symbol"]];
        if (type == "Put" || type == "Call")
        {
            bool rc = ParseONEOptionSpec(symbol, @"(\w+) +(.+)$", out position.symbol, out position.optionType, out position.expiration, out position.strike);
            if (!rc)
                return null;

            // confirm by parsing Expiry field
            string exp = fields[one_position_columns["Expiry"]];
            if (DateOnly.TryParse(exp, out DateOnly expiry))
            {
                if (position.expiration.CompareTo(expiry) != 0)
                {
                    if (expiry.AddDays(1) == position.expiration)
                        position.expiration = expiry;
                    else
                    {
                        Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has discrepency between date in Symbol field {position.expiration} and date in Expiry field {expiry}");
                        return null;
                    }
                }
            }
        }
        else if (type == "Stock")
        {
            position.symbol = symbol;
            position.optionType = SecurityType.Stock;
            position.expiration = new DateOnly(1, 1, 1);
            position.strike = 0;
        }
        else
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has invalid type field (Must be Put, Call, or Stock): {type}");
            return null;
        }

        string open_price = fields[one_position_columns["Price"]];
        if (!float.TryParse(open_price, out position.open_price))
        {
            Console.WriteLine($"\n***Error*** ONE Trade line #{line_index + 1} has invalid price field: {open_price}");
            return null;
        }

        return position;
    }

    // SPX APR2022 4300 P[SPXW  220429P04300000 100]
    static bool ParseONEOptionSpec(string field, string regex, out string symbol, out SecurityType type, out DateOnly expiration, out int strike)
    {
        symbol = "";
        type = SecurityType.Put;
        expiration = new();
        strike = 0;

        MatchCollection mc = Regex.Matches(field, regex);
        if (mc.Count < 1)
            return false;
        Match match0 = mc[0];
        if (match0.Groups.Count != 3)
            return false;

        symbol = match0.Groups[1].Value.Trim();
        string option_code = match0.Groups[2].Value;
        int year = int.Parse(option_code[0..2]) + 2000;
        int month = int.Parse(option_code[2..4]);
        int day = int.Parse(option_code[4..6]);
        expiration = new(year, month, day);
        type = (option_code[6] == 'P') ? SecurityType.Put : SecurityType.Call;
        strike = int.Parse(option_code[7..12]);
        return true;
    }

    static bool CompareONEPositionsToTDAPositions()
    {
        // verify that ONE Index position (if any) matches TDA Stock, Futures positons
        bool rc = VerifyStockPositions();

        // go through each consolidated ONE option position (whose quantity is != 0) and find it's associated TDA Position
        foreach ((OptionKey one_key, (int one_quantity, HashSet<string> one_trade_ids)) in consolidatedOnePositions)
        {
            if (one_quantity == 0)
                continue;

            Debug.Assert(one_key.OptionType != SecurityType.Futures);

            // if ONE position is Stock ignore it...already checked in call to VerifyStockPositions();
            if (one_key.OptionType == SecurityType.Stock)
                continue;

            var tda_position = new TDAPosition(one_key);
            if (!tdaPositions.Contains(tda_position))
            {
                Console.WriteLine($"\n***Error*** ONE has a {one_key.OptionType} position in trade(s) {string.Join(",", one_trade_ids)}, with no matching position in TDA:");
                Console.WriteLine($"{one_key.Symbol}\t{one_key.OptionType}\tquantity: {one_quantity}\texpiration: {one_key.Expiration}\tstrike: {one_key.Strike}");
                rc = false;
                continue;
            }

            if (one_quantity != tda_position.Quantity)
            {
                Console.WriteLine($"\n***Error*** ONE has a {one_key.OptionType} position in trade(s) {string.Join(",", one_trade_ids)}, whose quantity ({one_quantity}) does not match TDA quantity ({tda_position.Quantity}):");
                Console.WriteLine($"{one_key.Symbol}\t{one_key.OptionType}\tquantity: {one_quantity}\texpiration: {one_key.Expiration}\tstrike: {one_key.Strike}");
                rc = false;
            }

            // save one position reference in TDA position
            tda_position.oneTrades = one_trade_ids;

            // add one_position quantity to accounted_for_quantity...this will be checked later
            tda_position.one_quantity += one_quantity;
        }

        // ok...we've gone through all the ONE option positions, and tried to find associated TDA positions. But...
        // there could still be TDA option positions that have no corresponding ONE position
        // loop through all TDA option positions, find associated ONE positions (if they don't exist, display error)
        foreach (TDAPosition position in tdaPositions)
        {
            // ignore stock/futures positions...they've already been checked in VerifyStockPositions()
            if (position.Type == SecurityType.Stock || position.Type == SecurityType.Futures)
                continue;

            if (position.one_quantity != position.Quantity)
            {
                if (position.one_quantity == 0)
                {
                    Console.WriteLine($"\n***Error*** TDA has a {position.Type} position with no matching position in ONE");
                    DisplayTDAPosition(position);
                    rc = false;
                }
            }
        }

        return rc;
    }

    // make sure that any Index position in ONE is matched by stock/futures positionin TDA and vice versa
    static bool VerifyStockPositions()
    {
        // get ONE consolidated index position
        // note that net ONE index position could be 0 even if individual ONE trades have index positions
        List<OptionKey> one_stock_keys = consolidatedOnePositions.Keys.Where(s => s.OptionType == SecurityType.Stock).ToList();
        Debug.Assert(one_stock_keys.Count <= 1, "***Program Error*** VerifyStockPositions: more than 1 Index position in consolidatedOnePositions");
        (int one_quantity, HashSet<string> one_trade_ids) = (0, new());
        if (one_stock_keys.Count == 1)
        {
            OptionKey one_key = one_stock_keys[0];
            (one_quantity, one_trade_ids) = consolidatedOnePositions[one_key];
            Debug.Assert(one_quantity != 0);
        }

        // get TDA stock/futures positions
        // note that net TDA position could be 0 even if stock/futures positions exist in TDA
        List<TDAPosition> tda_stock_or_futures_positions = tdaPositions.Where(s => s.Type == SecurityType.Stock || s.Type == SecurityType.Futures).ToList();
        float tda_stock_or_futures_quantity = 0f;
        foreach (TDAPosition tda_stock_or_futures_position in tda_stock_or_futures_positions)
        {
            Dictionary<string, float> possible_tda_symbols = associated_symbols[master_symbol];
            Debug.Assert(possible_tda_symbols.ContainsKey(tda_stock_or_futures_position.Symbol));
            float multiplier = possible_tda_symbols[tda_stock_or_futures_position.Symbol];
            tda_stock_or_futures_quantity += multiplier * tda_stock_or_futures_position.Quantity;
        }

        if (one_quantity == tda_stock_or_futures_quantity)
            return true;

        // at this point, ONE's net Index position does not match TDA's net stock/futures position. 
        // note that either position could be 0
        if (tda_stock_or_futures_quantity == 0)
        {
            Debug.Assert(one_quantity != 0);
            Debug.Assert(one_trade_ids.Count > 0);
            Console.WriteLine($"\n***Error*** ONE has an index position in {master_symbol} of {one_quantity} shares, in trade(s) {string.Join(",", one_trade_ids)}, while TDA has no matching positions");
            return false;
        }

        if (one_quantity == 0)
        {
            Debug.Assert(tda_stock_or_futures_quantity > 0);
            Console.WriteLine($"\n***Error*** TDA has stock/futures positions of {tda_stock_or_futures_quantity} equivalent {master_symbol} shares, while ONE has no matching positions");
            // todo: list TDA positions
            return false;
        }

        // at this point, both ONE and TDA have index positions...just not same quantity

        Debug.Assert(one_stock_keys.Count == 1);
        Debug.Assert(one_quantity != 0);
        Debug.Assert(tda_stock_or_futures_quantity != one_quantity);
        Console.WriteLine($"\n***Error*** ONE has an index position in {master_symbol} of {one_quantity} shares, in trade(s) {string.Join(",", one_trade_ids)}, while TDA has {tda_stock_or_futures_quantity} equivalent {master_symbol} shares");
        return false;
    }

    const char delimiter = ',';
    static bool ParseCSVLine(int line_index, int index_of_last_required_column, string line, out List<string> fields)
    {
        Debug.Assert(line.Length > 0);

        fields = new();
        int state = 0;
        int start = 0;
        char c = '\0';
        char prevc = '\0';
        for (int i = 0; i < line.Length; i++)
        {
            prevc = c;
            c = line[i];
            switch (state)
            {
                case 0: // start of field; quote, delimiter, or other
                    switch (c)
                    {
                        case delimiter: // first char is delimiter...field is empty
                            fields.Add("");
                            break;
                        case '"': // field starts with quote
                            start = i + 1;
                            state = 2;
                            break;
                        default: // field starts with non-quote
                            start = i;
                            state = 1;
                            break;
                    }
                    break;

                case 1: // looking for end of field that didn't start with quote (interior quotes ignored)
                    if (c == delimiter)
                    {
                        fields.Add(line[start..i].Trim());
                        state = 0;
                    }
                    break;

                case 2: // looking for end of field that started with quote; if this is quote, could be start of double quote or end of field
                    if (c == '"')
                        state = 3;
                    break;

                case 3: // looking for end of field that started with quote; prior char was quote (that didn't start field)...if this is quote, it's a double quote, else better be delimiter to end field
                    if (c == '"')
                    {
                        // double quote...throw away first one
                        line = line[..i] + line[(i + 1)..];
                        i--;
                        state = 2;
                    }
                    else
                    {
                        if (c != delimiter)
                            return false; // malformed field
                        fields.Add(line.Substring(start, i - start - 1).Trim());
                        state = 0;
                    }
                    break;

                default:
                    Debug.Assert(false);
                    break;
            }

        }

        // process last field
        switch (state)
        {
            case 0:
                break;

            case 1: // field started with non-quote...standard end
                fields.Add(line[start..].Trim());
                break;

            case 2: // field started with quote, but didn't end with quote...error
                Console.WriteLine($"\n***Error*** In TDA file, line {line_index + 1} is not a valid comma separated line: {line}");
                return false;

            case 3: // field ended with quote
                string dbg = line[start..^1];
                fields.Add(line[start..^1].Trim());
                break;

            default:
                Debug.Assert(false);
                return false;
        }

        return true;
    }

    //static Dictionary<(string, OptionType, DateOnly, int), (int, HashSet<string>)> consolidatedOnePositions = new();
    static void DisplayONEPositions()
    {
        Console.WriteLine($"\nConsolidated ONE Positions for {master_symbol}:");

        foreach ((OptionKey one_key, (int quantity, HashSet<string> trades)) in consolidatedOnePositions)
        {
            switch (one_key.OptionType)
            {
                case SecurityType.Stock:
                    Console.WriteLine($"{one_key.Symbol}\tIndex\tquantity: {quantity}\ttrade(s): {string.Join(",", trades)}");
                    break;
                case SecurityType.Call:
                case SecurityType.Put:
                    // create trades list
                    Console.WriteLine($"{one_key.Symbol}\t{one_key.OptionType}\tquantity: {quantity}\texpiration: {one_key.Expiration}\tstrike: {one_key.Strike}\ttrade(s): {string.Join(",", trades)}");
                    break;
            }
        }
        Console.WriteLine();
    }

    static void DisplayTDAPositions()
    {
        Console.WriteLine($"TDA Positions related to {master_symbol}:");
        foreach (TDAPosition position in tdaPositions)
            DisplayTDAPosition(position);
    }

    static void DisplayIrrelevantTDAPositions()
    {
        Console.WriteLine($"\nTDA Positions **NOT** related to {master_symbol}:");
        foreach (TDAPosition position in irrelevantTDAPositions)
            DisplayTDAPosition(position);
    }

    static void DisplayTDAPosition(TDAPosition position)
    {
        if (position.Quantity == 0)
            return;

        switch (position.Type)
        {
            case SecurityType.Stock:
                //Console.WriteLine($"{position.symbol} {position.optionType}: quantity = {position.quantity}");
                Console.WriteLine($"{position.Symbol}\t{position.Type}\tquantity: {position.Quantity}");
                break;
            case SecurityType.Futures:
                //Console.WriteLine($"{position.symbol} {position.optionType}: expiration = {position.expiration}, quantity = {position.quantity}");
                Console.WriteLine($"{position.Symbol}\t{position.Type}\tquantity: {position.Quantity}\texpiration: {position.Expiration}");
                break;
            case SecurityType.Call:
            case SecurityType.Put:
                //Console.WriteLine($"{position.symbol} {position.optionType}: expiration = {position.expiration}, strike = {position.strike}, quantity = {position.quantity}");
                Console.WriteLine($"{position.Symbol}\t{position.Type}\tquantity: {position.Quantity}\texpiration: {position.Expiration}\tstrike: {position.Strike}");
                break;
        }
    }
}

