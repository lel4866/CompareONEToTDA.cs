# CompareONEToTDA.cs

This program compares exported data from OptioneNet Explorer (ONE) to that from TD Ameritrade(TDA) to make
sure that the option positions actually held in TDA are the ones that are beng modeled by ONE.

The program currently supports portfolios that contain index options for SPX, RUT, and NDX, stock positions in SPY, IWM, and QQQ, and
futures positoons in ES, MES, RTY, M2K, NQ, and MNQ. However analysis can only been done on one class of positions at a time as specified
by the command line --symbol parameter (which can be spx, rut, or ndx).

**This program has not been thoroughly tested and likely has bugs. In addition, the data formats used by TDA and ONE
can change without notice, which could cause the program to crash or produce erroneous results. Use at your own risk!**

## Command Line
This program is run from the command line, so you must open a terminal and either have this program on your PATH or change
the current working directory to the one containing this program.

The required command line arguments are --symbol, --onedir and --tdadir.  

**--symbol** specifies the index whose options will be checked. spx, rut, and ndx are currently supported. This
also determines the symbols for the stock/futures positions that will be taken into account.

**--onefile** specifies the name (including path if necessary) of the ONE file to be processed.

**--onedir** can be used instead of --onefile. It specifies a directory where your ONE files are saved.

**--tdafile** specifies the name (including path if necessary) of the TDA file to be processed.

**--tdadir** can be used instead of --tdafile, It specifies a directory where your TDA files are saved.

If you specify a directory (--onedir, --tdadir) instead of a file, the program will use the latest file in the directory whose name matches 
the proper pattern (yyyy-mm-dd-ONEDetailReport.csv for ONE, and yyyy-mm-dd-PositionStatement.csv for TDA). 

There are two optional command line arguments:

**--version** just displays the version of the program.

**--help** displays a short summary of the command line arguments.

There are short names for each of the commands: -s, -of, -od, -tf, -td, -v, and -h.

Sample command lines (from Windows Command Prompt):
```
CompareONEToTDA.exe -s spx -td C:\Users\username\TDAExport -od C:\Users\username\ONEExport > output.txt
CompareONEToTDA.exe --symbol spx --tdadir C:\Users\username\TDAExport --onedir C:\Users\username\ONEExport
```

Sample command lines (from Windows Power Shell):
```
./CompareONEToTDA.exe -s spx -td C:\Users\username\TDAExport -od C:\Users\username\ONEExport > output.txt
./CompareONEToTDA.exe --symbol spx --tdadir C:\Users\username\TDAExport --onedir C:\Users\username\ONEExport
```

Why specify the directories instead of the actual files? So you can just save newer files to the 
directory without changing the command line arguments. The program automatically selects 
the files with the latest dates embedded in the filenames (It does not check the actual OS time stamp). 

## Exporting the TDA data

The TDA data is exported by running Think or Swim, selecting the Monitor tab, then the Activities and Positions tab, 
then clicking on the menu icon on the far right of the Position Statement window, and then selecting Export to File...

### This is what the TDA data looks like:

```
Position Statement for 123456789 (userid) on 12/29/21 11:49:04

None
Instrument,Qty,Days,Trade Price,Mark,Mrk Chng,P/L Open,P/L Day,BP Effect
/MES,+1,,4777.50,4776.25,-2.25,($6.25),($6.25),"($1,265.00)"
"Micro E-mini S&P 500, Mar-22 (prev. /MESH2)",+1,79,4777.50,4776.25,-2.25,($6.25),($6.25),
SPX,,,,,,($330.00),($330.00),$0.00
S&P 500 INDEX,0,,.00,4784.37,-1.98,$0.00,$0.00,
100 21 JAN 22 4795 PUT,+1,22,63.50,63.20,N/A,($30.00),($30.00),
100 (Quarterlys) 31 MAR 22 4795 CALL,+2,92,141.30,140.25,-4.85,($210.00),($210.00),
100 (Weeklys) 31 MAY 22 4650 PUT,-3,153,176.10,179.90,N/A,"($1,140.00)","($1,140.00)",
100 (Weeklys) 31 MAY 22 4730 PUT,+3,153,199.80,204.00,N/A,"$1,260.00","$1,260.00",
100 (Weeklys) 31 MAY 22 4775 CALL,+2,153,223.10,214.40,N/A,"($1,740.00)","($1,740.00)",
100 (Weeklys) 31 MAY 22 4800 CALL,-2,153,206.60,198.95,-4.70,"$1,530.00","$1,530.00",
SPY,,,,,,$97.00,$97.00,"$23,828.00"
SPDR S&P500 ETF TRUST TR UNIT ETF,+100,,476.74,476.56,-.31,($18.00),($18.00),
100 18 JAN 22 477 PUT,+10,20,5.25,5.365,+.285,$115.00,$115.00,

Cash & Sweep Vehicle,"$2,310.40"
OVERALL P/L YTD,$60.75
BP ADJUSTMENT,$0.00
OVERNIGHT FUTURES BP,"$24,873.40"
AVAILABLE DOLLARS,"$24,873.40"
```

## Exporting the ONE data

The ONE data is exported by opening ONE, clicking on Reports, then on the Reports window, clicking on the little filter icon on the Account dropdown
and selecting the account that holds the trades you want to compare with, then clicking the Export button and saving the file.
**Make sure that the Report Type dropdown is set to Detail.**

### This is what the ONE data looks like:

```
ONE Detail Report

Date/Time: 12/8/2021 08:28:42
Filter: [Account] = 'TDA1'
Grouping: Account

,Account,Expiration,TradeId,TradeName,Underlying,Status,TradeType,OpenDate,CloseDate,DaysToExpiration,DaysInTrade,Margin,Comms,PnL,PnLperc
,,Account,TradeId,Date,Transaction,Qty,Symbol,Expiry,Type,Description,Underlying,Price,Commission
TDA1 
,"TDA1",12/3/2021,285,"244+1lp 2021-10-11 11:37",SPX,Open,Custom,10/11/2021 11:37 AM,,53,58,158973.30,46.46,13780.74,8.67
,,"TDA1",285,10/11/2021 11:37:32 AM,Buy,2,SPX   220319P04025000,3/18/2022,Put,SPX Mar22 4025 Put,SPX,113.92,2.28
,,"TDA1",285,10/11/2021 11:37:32 AM,Buy,4,SPX   220319P02725000,3/18/2022,Put,SPX Mar22 2725 Put,SPX,12.8,4.56
,,"TDA1",285,10/11/2021 11:37:32 AM,Sell,4,SPX   220319P03725000,3/18/2022,Put,SPX Mar22 3725 Put,SPX,68.77,4.56
,,"TDA1",285,10/11/2021 3:58:48 PM,Buy,1,SPXW  211204P03000000,12/3/2021,Put,SPX Dec21 3000 Put,SPX,2.7,1.5
```

## This is sample output: 

```
>.\CompareONEToTDA.exe -s spx -td C:\Users\username\TDAExport -od C:\Users\username\ONEExport > output.txt

CompareONEToTDA Version 0.0.1, 2021-12-17. Processing trades for SPX

Processing ONE file: C:\Users\lel48\OneDrive\Documents\ONEExport\2021-12-14-ONEDetailReport.csv
Processing TDA file: C:\Users\lel48\OneDrive\Documents\TDAExport\2021-12-13-PositionStatement.csv

ONE Trades:

Trade 284:
SPX  Put     quantity: 2    expiration: 3/18/2022    strike: 3100
SPX  Put     quantity: -4   expiration: 3/18/2022    strike: 3625
SPX  Put     quantity: 2    expiration: 3/18/2022    strike: 3825

Trade 285:
SPX  Put     quantity: 4    expiration: 3/18/2022    strike: 2725
SPX  Put     quantity: -4   expiration: 3/18/2022    strike: 3775
SPX  Put     quantity: 2    expiration: 3/18/2022    strike: 4025
SPXW Put     quantity: 1    expiration: 4/29/2022    strike: 3250

Trade 287:
SPX  Index   quantity: -1
SPX  Put     quantity: 4    expiration: 4/14/2022    strike: 2775
SPX  Put     quantity: -4   expiration: 4/14/2022    strike: 3800
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 4075

Trade 288:
SPX  Put     quantity: 2    expiration: 1/21/2022    strike: 2450
SPXW Put     quantity: 2    expiration: 3/31/2022    strike: 3175
SPXW Put     quantity: -2   expiration: 3/31/2022    strike: 3825
SPXW Put     quantity: -2   expiration: 3/31/2022    strike: 3850
SPXW Put     quantity: 2    expiration: 3/31/2022    strike: 4100

Trade 294: Closed. No positions

Trade 296:
SPXW Put     quantity: 1    expiration: 1/31/2022    strike: 2800
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 3150
SPX  Put     quantity: -4   expiration: 4/14/2022    strike: 3900
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 4200

Trade 297:
SPXW Put     quantity: 2    expiration: 1/31/2022    strike: 2600
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 3500
SPX  Put     quantity: -4   expiration: 4/14/2022    strike: 4050
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 4325

Trade 298:
SPX  Put     quantity: 2    expiration: 2/18/2022    strike: 2350
SPXW Put     quantity: 2    expiration: 4/29/2022    strike: 3400
SPXW Put     quantity: -4   expiration: 4/29/2022    strike: 4000
SPXW Put     quantity: 2    expiration: 4/29/2022    strike: 4300

Trade 301:
SPX  Index   quantity: 5
SPXW Put     quantity: 1    expiration: 1/14/2022    strike: 2700
SPX  Put     quantity: 1    expiration: 1/21/2022    strike: 2850
SPXW Put     quantity: 1    expiration: 1/28/2022    strike: 3000
SPXW Put     quantity: 1    expiration: 1/31/2022    strike: 2750

Trade 302: Closed. No positions

Trade 303: Closed. No positions

Trade 304: Closed. No positions

Consolidated ONE Positions for SPX:
SPX  Index   quantity: 4    trade(s): 287,301
SPXW Put     quantity: 1    expiration: 1/14/2022    strike: 2700   trade(s): 301
SPX  Put     quantity: 2    expiration: 1/21/2022    strike: 2450   trade(s): 288
SPX  Put     quantity: 1    expiration: 1/21/2022    strike: 2850   trade(s): 301
SPXW Put     quantity: 1    expiration: 1/28/2022    strike: 3000   trade(s): 301
SPXW Put     quantity: 2    expiration: 1/31/2022    strike: 2600   trade(s): 297
SPXW Put     quantity: 1    expiration: 1/31/2022    strike: 2750   trade(s): 301
SPXW Put     quantity: 1    expiration: 1/31/2022    strike: 2800   trade(s): 296
SPX  Put     quantity: 2    expiration: 2/18/2022    strike: 2350   trade(s): 298
SPX  Put     quantity: 4    expiration: 3/18/2022    strike: 2725   trade(s): 285
SPX  Put     quantity: 2    expiration: 3/18/2022    strike: 3100   trade(s): 284
SPX  Put     quantity: -4   expiration: 3/18/2022    strike: 3625   trade(s): 284
SPX  Put     quantity: -4   expiration: 3/18/2022    strike: 3775   trade(s): 285
SPX  Put     quantity: 2    expiration: 3/18/2022    strike: 3825   trade(s): 284
SPX  Put     quantity: 2    expiration: 3/18/2022    strike: 4025   trade(s): 285
SPXW Put     quantity: 2    expiration: 3/31/2022    strike: 3175   trade(s): 288
SPXW Put     quantity: -2   expiration: 3/31/2022    strike: 3825   trade(s): 288
SPXW Put     quantity: -2   expiration: 3/31/2022    strike: 3850   trade(s): 288
SPXW Put     quantity: 2    expiration: 3/31/2022    strike: 4100   trade(s): 288
SPX  Put     quantity: 4    expiration: 4/14/2022    strike: 2775   trade(s): 287
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 3150   trade(s): 296
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 3500   trade(s): 297
SPX  Put     quantity: -4   expiration: 4/14/2022    strike: 3800   trade(s): 287
SPX  Put     quantity: -4   expiration: 4/14/2022    strike: 3900   trade(s): 296
SPX  Put     quantity: -4   expiration: 4/14/2022    strike: 4050   trade(s): 297
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 4075   trade(s): 287
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 4200   trade(s): 296
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 4325   trade(s): 297
SPXW Put     quantity: 1    expiration: 4/29/2022    strike: 3250   trade(s): 285
SPXW Put     quantity: 2    expiration: 4/29/2022    strike: 3400   trade(s): 298
SPXW Put     quantity: -4   expiration: 4/29/2022    strike: 4000   trade(s): 298
SPXW Put     quantity: 2    expiration: 4/29/2022    strike: 4300   trade(s): 298

TDA Positions related to SPX:
SPY  Stock   quantity: 100
MES  Futures quantity: 1    expiration: 3/1/2022
SPXW Put     quantity: 1    expiration: 1/14/2022    strike: 2700
SPX  Put     quantity: 2    expiration: 1/21/2022    strike: 2450
SPX  Put     quantity: 1    expiration: 1/21/2022    strike: 2850
SPXW Put     quantity: 1    expiration: 1/28/2022    strike: 3000
SPXW Put     quantity: 2    expiration: 1/31/2022    strike: 2600
SPXW Put     quantity: 1    expiration: 1/31/2022    strike: 2750
SPXW Put     quantity: 1    expiration: 1/31/2022    strike: 2800
SPX  Put     quantity: 2    expiration: 2/18/2022    strike: 2350
SPX  Put     quantity: 4    expiration: 3/18/2022    strike: 2725
SPX  Put     quantity: 2    expiration: 3/18/2022    strike: 3100
SPX  Put     quantity: -4   expiration: 3/18/2022    strike: 3625
SPX  Put     quantity: -4   expiration: 3/18/2022    strike: 3775
SPX  Put     quantity: 2    expiration: 3/18/2022    strike: 3825
SPX  Put     quantity: 2    expiration: 3/18/2022    strike: 4025
SPXW Put     quantity: 2    expiration: 3/31/2022    strike: 3175
SPXW Put     quantity: -2   expiration: 3/31/2022    strike: 3825
SPXW Put     quantity: -2   expiration: 3/31/2022    strike: 3850
SPXW Put     quantity: 2    expiration: 3/31/2022    strike: 4100
SPX  Put     quantity: 4    expiration: 4/14/2022    strike: 2775
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 3150
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 3350
SPX  Put     quantity: -4   expiration: 4/14/2022    strike: 3800
SPX  Put     quantity: -4   expiration: 4/14/2022    strike: 3900
SPX  Put     quantity: -4   expiration: 4/14/2022    strike: 4050
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 4075
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 4200
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 4325
SPXW Put     quantity: 2    expiration: 4/29/2022    strike: 3250
SPXW Put     quantity: -4   expiration: 4/29/2022    strike: 4000
SPXW Put     quantity: 2    expiration: 4/29/2022    strike: 4300

***Error*** ONE has an index position in SPX of 4 shares, in trade(s) 287,301, while TDA has 15 equivalent SPX shares

***Error*** ONE has a Put position in trade(s) 297, with no matching position in TDA:
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 3500

***Error*** ONE has a Put position in trade(s) 285, whose quantity (1) does not match TDA quantity (2):
SPXW Put     quantity: 1    expiration: 4/29/2022    strike: 3250

***Error*** ONE has a Put position in trade(s) 298, with no matching position in TDA:
SPXW Put     quantity: 2    expiration: 4/29/2022    strike: 3400

***Error*** TDA has a Put position with no matching position in ONE
SPX  Put     quantity: 2    expiration: 4/14/2022    strike: 3350
```
