using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CompareONEToTDA;

// todo: rename OptionType to SecurityType
public enum OptionType
{
    Put,
    Call,
    Stock,
    Futures
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
    public OptionType optionType;
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
    public OptionType OptionType { get; set; }
    public DateOnly Expiration { get; set; }
    public int Strike { get; set; }

    public OptionKey(string symbol, OptionType optionType, DateOnly expiration, int strike)
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

        bool thisIsOption = OptionType == OptionType.Put || OptionType == OptionType.Call;
        bool otherIsOption = other.OptionType == OptionType.Put || other.OptionType == OptionType.Call;
        if (!thisIsOption)
        {
            // this is stock/future

            if (otherIsOption)
                return -1; // this is stock/future, other is option: stocks/futures come before options

            // this and other are both Stocks/Futures: stocks come before futures, then symbol, then, if future, expiration

            if (OptionType == OptionType.Stock)
            {
                if (other.OptionType == OptionType.Futures)
                    return -1; // stocks come before futures

                // this and other are both stocks...sort by symbol
                return Symbol.CompareTo(other.Symbol);
            }

            // this is futures

            if (other.OptionType == OptionType.Stock)
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

//Financial Instrument Description, Position, Currency, Market Price, Market Value, Average Price, Unrealized P&L, Realized P&L, Liquidate Last, Security Type, Delta Dollars
//SPX APR2022 4300 P [SPXW  220429P04300000 100],2,USD,119.5072021,23901.44,123.5542635,-809.41,0.00,No,OPT,-246454.66
class TDAPosition
{
    public OptionType optionType; // just Put, Call, or Stock...futures are converted to equivalent SPX stock...so is SPY
    public string symbol = ""; // SPX, SPXW, etc
    public int strike = 0;
    public DateOnly expiration = new();
    public int quantity;

    // used only during reconciliation with ONE positions
    public int one_quantity = 0;
    public HashSet<string> oneTrades = new();
}

static class Program
{
    internal const string version = "0.0.3";
    internal const string version_date = "2022-012-06";
    internal static string? tda_filename = null;
    internal static string tda_directory = @"C:\Users\lel48\OneDrive\Documents\TDAExport\";
    internal static string? one_filename = null;
    internal static string one_directory = @"C:\Users\lel48\OneDrive\Documents\ONEExport\";
    internal static string master_symbol = "SPX";
    internal static string one_account = "";

    // ONE uses the main index symbol for positions in the underlying, whereas TDA uses an actual stock/futures symbol
    // so...to reconcile these, if ONE has a position, say, of 10 SPX, this could be equivalent to 2 MES contracts,
    // 100 SPY shares, or some combination, like 1 MES contract and 50 SPY shares
    // So...the float is the number of ONE SPX shares that a share of the given item represents. So, { "SPY", 0.1f }
    // means that 1 share of SPY in TDA represents 0.1 shares of SPX in ONE
    internal static Dictionary<string, Dictionary<string, float>> associated_symbols = new()
    {
        { "SPX", new Dictionary<string, float> { { "SPY", 0.1f }, { "MES", 5f }, { "ES", 50f } } },
        { "RUT", new Dictionary<string, float> { { "IWM", 0.1f }, { "M2K", 5f }, { "RTY", 50f } } },
        { "NDX", new Dictionary<string, float> { { "QQQ", 0.1f }, { "MNQ", 5f }, { "NQ", 50f } } }
    };

    // note: the ref is readonly, not the contents of the Dictionary
    static readonly Dictionary<string, int> tda_columns = new(); // key is column name, value is column index
    static readonly Dictionary<string, int> one_trade_columns = new(); // key is column name, value is column index
    static readonly Dictionary<string, int> one_position_columns = new(); // key is column name, value is column index
    static readonly Dictionary<string, ONETrade> oneTrades = new(); // key is trade_id

    // key is (symbol, OptionType, Expiration, Strike); value is quantity
    static readonly SortedDictionary<OptionKey, TDAPosition> tdaPositions = new();

    // dictionry of ONE trades with key of trade id
    static readonly SortedDictionary<string, ONETrade> ONE_trades = new();

    // consolidated ONE positions; key is (symbol, OptionType, Expiration, Strike); value is (quantity, HashSet<string>); string is trade id 
    static readonly SortedDictionary<OptionKey, (int, HashSet<string>)> consolidatedOnePositions = new();

    static int Main(string[] args)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        // calls System.Environment.Exit(-1) if bad command line arguments
        CommandLine.ProcessCommandLineArguments(args);
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

    //Portfolio
    //Financial Instrument Description, Position, Currency, Market Price, Market Value, Average Price, Unrealized P&L, Realized P&L, Liquidate Last, Security Type, Delta Dollars
    //SPX APR2022 4300 P [SPXW  220429P04300000 100],2,USD,119.5072021,23901.44,123.5542635,-809.41,0.00,No,OPT,-246454.66
    static bool ProcessTDAFile(string full_filename)
    {
        const string simulated_position_header = "This document was exported from the paperMoney";

        List<string> lines = ReadAllNonBlankLines(full_filename);        
        if (lines.Count < 5)
        {
            Console.WriteLine("\n***Error*** TDA File must contain at least 5 non-blank lines");
            return false;
        }

        int line_index = 0;
        string line = lines[line_index++];
        if (line.StartsWith(simulated_position_header)) {
            Console.WriteLine($"\n***Warning*** TDA file starts with line containing phrase '{simulated_position_header}', which indicates these are just simulated positions.");
            line = lines[line_index++];
        }

        if (!line.StartsWith("Position Statement for")) {
            Console.WriteLine("***\nError*** TDA file must begin with line containing the phrase 'Position Statement for...'");
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
        int index_of_last_required_column = 0;
        for (int i = 0; i < required_columns.Length; i++)
        {
            if (!tda_columns.TryGetValue(required_columns[i], out int colnum))
            {
                Console.WriteLine($"\n***Error*** TDA file header must contain column named {required_columns[i]}");
                return false;
            }
            index_of_last_required_column = Math.Max(colnum, index_of_last_required_column);
        }

        // now process each TDA position line
        while (line_index  < lines.Count)
        {
            line = lines[line_index++];
            bool rc = ParseCSVLine(line, out List<string> fields);
            if (!rc)
                return false;

            // blank line terminates list of positions. Next line must be "Cash Balances"
            if (fields.Count == 0)
                break;

            if (fields.Count < index_of_last_required_column + 1)
            {
                Console.WriteLine($"\n***Error*** TDA position line {line_index + 1} must have {index_of_last_required_column + 1} fields, not {fields.Count} fields");
                return false;
            }

            int irc = ParseTDAPositionLine(line_index, fields);
            if (irc != 0)
            {
                // if irc == -1, error parsing line
                if (irc < 0)
                    return false;
                // irc +1, irrelevant symbol - ignore line
            }
        }

        return true;
    }

    static List<string> ReadAllNonBlankLines(string filename)
    {
        List<string> lines = new();
        using StreamReader reader = new StreamReader(filename);
        while (true) 
        {
            string? line = reader.ReadLine();
            if (line == null)
                break;
            line = line.Trim();
            if (line.Length == 0)
                continue;
            lines.Add(line);
        }
        return lines;
    }

    //Financial Instrument Description, Position, Currency, Market Price, Market Value, Average Price, Unrealized P&L, Realized P&L, Liquidate Last, Security Type, Delta Dollars
    //SPX APR2022 4300 P [SPXW  220429P04300000 100],2,USD,119.5072021,23901.44,123.5542635,-809.41,0.00,No,OPT,-246454.66
    //SPY,100,USD,463.3319397,46333.19,463.02,31.19,0.00,No,STK,46333.19
    //MES MAR2022,1, USD,4624.50,23122.50,4625.604,-5.52,0.00, No, FUT,23136.14

    // returns 0 if line was parsed successfully, -1 if there was an error, 1 if line parsed ok, but is for symbol not relevant to this analysis
    static int ParseTDAPositionLine(int line_index, List<string> fields)
    {
        TDAPosition tdaPosition = new();

        int quantity_col = tda_columns["Qty"];
        bool rc = int.TryParse(fields[quantity_col], out tdaPosition.quantity);
        if (!rc)
        {
            Console.WriteLine($"***Error*** in #{line_index + 1} in TDA file: invalid Position: {fields[quantity_col]}");
            return -1;
        }

        int instrument_col = tda_columns["Instrument"];
        string instrument = fields[instrument_col];

        int security_type_col = tda_columns["Security Type"];
        string security_type = fields[security_type_col].Trim();
        switch (security_type)
        {
            case "OPT":
                rc = ParseOptionSpec(instrument, @".*\[(\w+) +(.+) \w+\]$", out tdaPosition.symbol, out tdaPosition.optionType, out tdaPosition.expiration, out tdaPosition.strike);
                if (!rc)
                {
                    Console.WriteLine($"***Error*** in #{line_index + 1} in TDA file: invalid option specification: {fields[instrument_col]}");
                    return -1;
                }

                break;

            case "FUT":
                //MES      MAR2022,1,USD,4624.50,23122.50,4625.604,-5.52,0.00,No,FUT,23136.14
                tdaPosition.optionType = OptionType.Futures;
                rc = ParseFuturesSpec(instrument, @"(\w+) +(\w+)$", out tdaPosition.symbol, out tdaPosition.expiration);
                break;

            case "STK":
                //SPY,100,USD,463.3319397,46333.19,463.02,31.19,0.00,No,STK,46333.19
                tdaPosition.optionType = OptionType.Stock;
                tdaPosition.symbol = fields[0].Trim();
                break;
        }

        var tda_key = new OptionKey(tdaPosition.symbol, tdaPosition.optionType, tdaPosition.expiration, tdaPosition.strike);
        if (tdaPositions.ContainsKey(tda_key))
        {
            if (tdaPosition.optionType == OptionType.Put || tdaPosition.optionType == OptionType.Call)
            {
                Console.WriteLine($"***Error*** in #{line_index + 1} in TDA file: duplicate expiration/strike ({tdaPosition.symbol} {tdaPosition.optionType} {tdaPosition.expiration},{tdaPosition.strike})");
                return -1;
            }
            else
            {
                if (tdaPosition.optionType == OptionType.Futures)
                    Console.WriteLine($"***Error*** in #{line_index + 1} in TDA file: duplicate futures entry ({tdaPosition.symbol} {tdaPosition.expiration})");
                else
                    Console.WriteLine($"***Error*** in #{line_index + 1} in TDA file: duplicate stock entry ({tdaPosition.symbol})");
                return -1;
            }
        }
        tdaPositions.Add(tda_key, tdaPosition);
        return 0;
    }

    //MES      MAR2022,1,USD,4624.50,23122.50,4625.604,-5.52,0.00,No,FUT,23136.14
    static bool ParseFuturesSpec(string field, string regex, out string symbol, out DateOnly expiration)
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

    // SPX APR2022 4300 P[SPXW  220429P04300000 100]
    static bool ParseOptionSpec(string field, string regex, out string symbol, out OptionType type, out DateOnly expiration, out int strike)
    {
        symbol = "";
        type = OptionType.Put;
        expiration = new();
        strike = 0;

        MatchCollection mc = Regex.Matches(field, regex);
        if (mc.Count > 1)
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
        type = (option_code[6] == 'P') ? OptionType.Put : OptionType.Call;
        strike = int.Parse(option_code[7..12]);
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
            bool rc = ParseCSVLine(line, out List<string> fields);
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
                    case OptionType.Stock:
                        Console.WriteLine($"{master_symbol}\tIndex\tquantity: {quantity}");
                        break;
                    case OptionType.Put:
                    case OptionType.Call:
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
            bool rc = ParseOptionSpec(symbol, @"(\w+) +(.+)$", out position.symbol, out position.optionType, out position.expiration, out position.strike);
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
            position.optionType = OptionType.Stock;
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

    static bool CompareONEPositionsToTDAPositions()
    {
        // verify that ONE Index position (if any) matches TDA Stock, Futures positons
        bool rc = VerifyStockPositions();

        // go through each consolidated ONE option position (whose quantity is != 0) and find it's associated TDA Position
        foreach ((OptionKey one_key, (int one_quantity, HashSet<string> one_trade_ids)) in consolidatedOnePositions)
        {
            if (one_quantity == 0)
                continue;

            Debug.Assert(one_key.OptionType != OptionType.Futures);

            // if ONE position is Stock ignore it...already checked in call to VerifyStockPositions();
            if (one_key.OptionType == OptionType.Stock)
                continue;

            if (!tdaPositions.TryGetValue(one_key, out TDAPosition? tda_position))
            {
                Console.WriteLine($"\n***Error*** ONE has a {one_key.OptionType} position in trade(s) {string.Join(",", one_trade_ids)}, with no matching position in TDA:");
                Console.WriteLine($"{one_key.Symbol}\t{one_key.OptionType}\tquantity: {one_quantity}\texpiration: {one_key.Expiration}\tstrike: {one_key.Strike}");
                rc = false;
                continue;
            }

            if (one_quantity != tda_position.quantity)
            {
                Console.WriteLine($"\n***Error*** ONE has a {one_key.OptionType} position in trade(s) {string.Join(",", one_trade_ids)}, whose quantity ({one_quantity}) does not match TDA quantity ({tda_position.quantity}):");
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
        foreach (TDAPosition position in tdaPositions.Values)
        {
            // ignore stock/futures positions...they've already been checked in VerifyStockPositions()
            if (position.optionType == OptionType.Stock || position.optionType == OptionType.Futures)
                continue;

            if (position.one_quantity != position.quantity)
            {
                if (position.one_quantity == 0)
                {
                    Console.WriteLine($"\n***Error*** TDA has a {position.optionType} position with no matching position in ONE");
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
        List<OptionKey> one_stock_keys = consolidatedOnePositions.Keys.Where(s => s.OptionType == OptionType.Stock).ToList();
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
        List<OptionKey> tda_stock_or_futures_keys = tdaPositions.Keys.Where(s => s.OptionType == OptionType.Stock || s.OptionType == OptionType.Futures).ToList();
        float tda_stock_or_futures_quantity = 0f;
        foreach (OptionKey tda_stock_or_futures_key in tda_stock_or_futures_keys)
        {
            Dictionary<string, float> possible_tda_symbols = associated_symbols[master_symbol];
            Debug.Assert(possible_tda_symbols.ContainsKey(tda_stock_or_futures_key.Symbol));
            float multiplier = possible_tda_symbols[tda_stock_or_futures_key.Symbol];
            float quantity = tdaPositions[tda_stock_or_futures_key].quantity;
            tda_stock_or_futures_quantity += multiplier * quantity;
        }

        if (one_quantity == tda_stock_or_futures_quantity)
            return true;

        // at this point, ONE's net Index position does not match TDA's net stock/futures position. 
        // note that either position could be 0
        if (tda_stock_or_futures_quantity == 0)
        {
            Debug.Assert(one_quantity > 0);
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
    static bool ParseCSVLine(string line, out List<string> fields)
    {
        fields = new();
        int state = 0;
        int start = 0;
        char c;
        for (int i = 0; i < line.Length; i++)
        {
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
            case 0: // must be blank line
                Debug.Assert(line.Length == 0);
                break;

            case 1: // field started with non-quote...standard end
                fields.Add(line[start..].Trim());
                break;

            case 2: // field started with quote, but didn't end with quote...error
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
                case OptionType.Stock:
                    Console.WriteLine($"{one_key.Symbol}\tIndex\tquantity: {quantity}\ttrade(s): {string.Join(",", trades)}");
                    break;
                case OptionType.Call:
                case OptionType.Put:
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
        foreach (TDAPosition position in tdaPositions.Values)
            DisplayTDAPosition(position);
        //Console.WriteLine();
    }

    static void DisplayTDAPosition(TDAPosition position)
    {
        if (position.quantity == 0)
            return;

        switch (position.optionType)
        {
            case OptionType.Stock:
                //Console.WriteLine($"{position.symbol} {position.optionType}: quantity = {position.quantity}");
                Console.WriteLine($"{position.symbol}\t{position.optionType}\tquantity: {position.quantity}");
                break;
            case OptionType.Futures:
                //Console.WriteLine($"{position.symbol} {position.optionType}: expiration = {position.expiration}, quantity = {position.quantity}");
                Console.WriteLine($"{position.symbol}\t{position.optionType}\tquantity: {position.quantity}\texpiration: {position.expiration}");
                break;
            case OptionType.Call:
            case OptionType.Put:
                //Console.WriteLine($"{position.symbol} {position.optionType}: expiration = {position.expiration}, strike = {position.strike}, quantity = {position.quantity}");
                Console.WriteLine($"{position.symbol}\t{position.optionType}\tquantity: {position.quantity}\texpiration: {position.expiration}\tstrike: {position.strike}");
                break;
        }
    }
}

