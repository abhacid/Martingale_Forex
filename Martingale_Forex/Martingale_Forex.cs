#region Licence
//The MIT License (MIT)
//Copyright (c) 2014 abdallah HACID, https://www.facebook.com/ab.hacid

//Permission is hereby granted, free of charge, to any person obtaining a copy of this software
//and associated documentation files (the "Software"), to deal in the Software without restriction,
//including without limitation the rights to use, copy, modify, merge, publish, distribute,
//sublicense, and/or sell copies of the Software, and to permit persons to whom the Software
//is furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all copies or
//substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
//BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
//DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

// Project Hosting for Open Source Software on Github : https://github.com/abhacid/Robot_Forex
#endregion


#region Description
//
// Le projet et sa description se trouvent sur Github à l'adresse https://github.com/abhacid/Martingale_Forex
//
// Ce projet permet d'écrire un robot de trading basé sur un exemple Robot_Forex initial écrit par 
// imWald sur le dépôt de code source CTDN.

// C'est suite à une demande d'un trader Antoine C qui souhaitait ajouter un stop-Loss à ce robot que
// j'ai commencé l'étude puis la modification de ce dernier. De fil en aiguille j'y ai ajouté d'autres 
// caractéristiques et améliorations.

// Ce robot est une martingale cela signifie qu'il augmente le volume de ses prises de positions
// lorsqu'il perd afin de moyenner à la hausse ou à la baisse selon qu'il achète ou qu'il vend. 
// le coefficient de martingale martingaleCoeff permet de définir le multiplicateur de volume; 
// s'il est à 1 le volume augmente d'une unité du volume initial FirstLot ou de façon générale 
// il augmente de martingaleCoeff*FirstLot.

//Un stop loss et un take profit est appliqué sur chacune des positions mais en se basant sur 
//le prix moyen, c'est à dire le barycentre des prises de positions : somme(prix*volume)/somme(volume).

// Pour décider à partir de quel niveau de perte on prends une nouvelle position, on estime 
// l'écart prix max-prix min sur une période de '25.0/(Nombre de positions)' bougies et on divise cet écart 
// par MaxOrders ce qui donne une grille dont l'écartement varie en fonction de la volatilité des prix, elle 
// est de plus en plus resserrée lorsque la volatilité diminue. 


// Nom : Martingale_Forex

// Paramètres du robot
// Money Management (%) : représente le risque maximum en % de perte du capital initial du compte de trading,
// Take Profit			: représente le profit en PIPS si la position est gagnante sans utilisation de la martingale,
// Stop Loss Factor		: représente le rapport (Stop Loss)/(Take Profit),
// Martingale			: représente le coefficient de martingale : (nouveau volume) = (premier volume)*(1+Martingale),
// Max Orders			: représente le nombre maximum de positions ouvertes.

// Epreuve (il ne s'agit pas des meilleurs paramètres):
// Symbol				= EURUSD,
// Timeframe			= m5,
// Version				= 1.3.1.2
// Money Management (%) = 3 
// Take Profit			= 10
// Stop Loss Factor		= 5
// Martingale			= 0.3 
// Max Orders			= 6

// Backtests : 
// plateforme		:	cAlgo version 1.30.58489
// Robot			:	Martingale_Forex v1.3.1.2
// Capital initial	:	10 000€
// flux de données	:	Tick data from server (accurate)
// commission		:	30 per Million
// Résultats		:	50 409 069€
// entre le 01 Mai 2015 et le 21 Juin 2015 gain de 3830€.

#endregion

using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Lib;

namespace cAlgo.Robots
{
    [Robot("Martingale Forex", AccessRights = AccessRights.None)]
    public class Martingale_Forex : Robot
    {
        #region Parameters
        [Parameter("Version", DefaultValue = "1.3.1.2")]
        public string BotVersion { get; set; }

        [Parameter("Money Management (%)", DefaultValue = 3, MinValue = 0)]
        public double MoneyManagement { get; set; }

        [Parameter("Take Profit", DefaultValue = 10, MinValue = 5)]
        public double TakeProfit { get; set; }

        [Parameter("Stop Loss Factor", DefaultValue = 5, MinValue = 0.1)]
        public double StopLossFactor { get; set; }

        [Parameter("Martingale", DefaultValue = 0.3, MinValue = 0)]
        public double MartingaleCoeff { get; set; }

        [Parameter("Max Orders", DefaultValue = 6, MinValue = 2)]
        public int MaxOrders { get; set; }

        #endregion

        private bool isRobotStopped;
        private string botName;
        // le label permet de s'y retrouver parmis toutes les instances possibles.
        private string instanceLabel;

        private double stopLoss;
        private double firstLot;
        private StaticPosition corner_position;


        protected override void OnStart()
        {
            botName = ToString();
            instanceLabel = botName + "-" + BotVersion + "-" + Symbol.Code + "-" + TimeFrame.ToString();

            stopLoss = TakeProfit * StopLossFactor;
            Positions.Opened += OnPositionOpened;

            int corner = 1;

            switch (corner)
            {
                case 1:
                    corner_position = StaticPosition.TopLeft;
                    break;
                case 2:
                    corner_position = StaticPosition.TopRight;
                    break;
                case 3:
                    corner_position = StaticPosition.BottomLeft;
                    break;
                case 4:
                    corner_position = StaticPosition.BottomRight;
                    break;
            }

            ChartObjects.DrawText("BotVersion", botName + " Version : " + BotVersion, corner_position);

            Print("The current symbol has PipSize of: {0}", Symbol.PipSize);
            Print("The current symbol has PipValue of: {0}", Symbol.PipValue);
            Print("The current symbol has TickSize: {0}", Symbol.TickSize);
            Print("The current symbol has TickSValue: {0}", Symbol.TickValue);
        }

        protected override void OnTick()
        {
            if (Trade.IsExecuting)
                return;

            Position[] positions = GetPositions();

            if (positions.Length > 0 && isRobotStopped)
                return;
            else
                isRobotStopped = false;

            if (positions.Length == 0)
            {
				// Calcule le volume en fonction du money management pour un risque maximum et un stop loss donné.
				// Ne tient pas compte des risques sur d'autres positions ouvertes du compte de trading utilisé
				double maxVolume = this.moneyManagement(MoneyManagement, stopLoss);
				firstLot = maxVolume / (MaxOrders + (MartingaleCoeff * MaxOrders * (MaxOrders - 1)) / 2.0);

                if (firstLot <= 0)
                    throw new System.ArgumentException(String.Format("the 'first lot' : {0} parameter must be positive and not null", firstLot));
                else
                    SendFirstOrder(firstLot);
            }
            else

                ControlSeries();
        }

        protected override void OnError(Error CodeOfError)
        {
            if (CodeOfError.Code == ErrorCode.NoMoney)
            {
                isRobotStopped = true;
                Print("ERROR!!! No money for order open, robot is stopped!");
            }
            else if (CodeOfError.Code == ErrorCode.BadVolume)
            {
                isRobotStopped = true;
                Print("ERROR!!! Bad volume for order open, robot is stopped!");
            }
        }

        private void SendFirstOrder(double OrderVolume)
        {
            switch (GetStdIlanSignal())
            {
                case 0:
                    executeOrder(TradeType.Buy, OrderVolume);
                    break;
                case 1:
                    executeOrder(TradeType.Sell, OrderVolume);
                    break;
            }
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            double? stopLossPrice = null;
            double? takeProfitPrice = null;

            switch (GetPositionsSide())
            {
                case 0:
                    double averageBuyPrice = GetAveragePrice(TradeType.Buy);
                    takeProfitPrice = averageBuyPrice + TakeProfit * Symbol.PipSize;
                    stopLossPrice = averageBuyPrice - stopLoss * Symbol.PipSize;
                    break;
                case 1:
                    double averageSellPrice = GetAveragePrice(TradeType.Sell);
                    takeProfitPrice = averageSellPrice - TakeProfit * Symbol.PipSize;
                    stopLossPrice = averageSellPrice + stopLoss * Symbol.PipSize;
                    break;
            }

            if (stopLossPrice.HasValue || takeProfitPrice.HasValue)
            {
                Position[] positions = GetPositions();

                foreach (Position position in positions)
                {
                    if (stopLossPrice != position.StopLoss || takeProfitPrice != position.TakeProfit)
                        ModifyPosition(position, stopLossPrice, takeProfitPrice);
                }
            }
        }

        private void ControlSeries()
        {
            Position[] positions = GetPositions();

            if (positions.Length < MaxOrders)
            {
                long volume = Symbol.NormalizeVolume(firstLot * (1 + MartingaleCoeff * positions.Length), RoundingMode.ToNearest);
                int countOfBars = (int)(25.0 / positions.Length);

                int pipstep = GetDynamicPipstep(countOfBars, MaxOrders + 1);
                int positionSide = GetPositionsSide();

                switch (positionSide)
                {
                    case 0:
                        double lastBuyPrice = FindLastPrice(TradeType.Buy);
                        //   ChartObjects.DrawHorizontalLine("gridBuyLine", lastBuyPrice - pipstep * Symbol.PipSize, Colors.Green, 2);
                        if (Symbol.Ask < lastBuyPrice - pipstep * Symbol.PipSize)
                            executeOrder(TradeType.Buy, volume);
                        break;

                    case 1:
                        double lastSellPrice = FindLastPrice(TradeType.Sell);
                        //      ChartObjects.DrawHorizontalLine("gridSellLine", lastSellPrice + pipstep * Symbol.PipSize, Colors.Red, 2);
                        if (Symbol.Bid > lastSellPrice + pipstep * Symbol.PipSize)
                            executeOrder(TradeType.Sell, volume);
                        break;
                }
            }

            ChartObjects.DrawText("MaxDrawdown", "MaxDrawdown: " + Math.Round(GetMaxDrawdown(), 2) + " Percent", corner_position);
        }

        // You can modify the condition of entry here.
        private int GetStdIlanSignal()
        {
            int Result = -1;
            int LastBarIndex = MarketSeries.Close.Count - 2;
            int PrevBarIndex = LastBarIndex - 1;

            // two up candles for a buy signal.
            if (MarketSeries.Close[LastBarIndex] > MarketSeries.Open[LastBarIndex])
                if (MarketSeries.Close[PrevBarIndex] > MarketSeries.Open[PrevBarIndex])
                    Result = 0;

            // two down candles for a sell signal.
            if (MarketSeries.Close[LastBarIndex] < MarketSeries.Open[LastBarIndex])
                if (MarketSeries.Close[PrevBarIndex] < MarketSeries.Open[PrevBarIndex])
                    Result = 1;

            return Result;
        }

        private TradeResult executeOrder(TradeType tradeType, double volume)
        {
            //Print("normalized volume : {0}", Symbol.NormalizeVolume(volume, RoundingMode.ToNearest));
            return ExecuteMarketOrder(tradeType, Symbol, Symbol.NormalizeVolume(volume, RoundingMode.ToNearest), instanceLabel);
        }

        /// <summary>
       

        private Position[] GetPositions()
        {
            return Positions.FindAll(instanceLabel, Symbol);
        }

        private double GetAveragePrice(TradeType TypeOfTrade)
        {
            double Result = Symbol.Bid;
            double AveragePrice = 0;
            long count = 0;

            foreach (Position position in GetPositions())
            {
                if (position.TradeType == TypeOfTrade)
                {
                    AveragePrice += position.EntryPrice * position.Volume;
                    count += position.Volume;
                }
            }

            if (AveragePrice > 0 && count > 0)
                Result = AveragePrice / count;

            return Result;
        }

        private int GetPositionsSide()
        {
            int Result = -1;
            int BuySide = 0, SellSide = 0;
            Position[] positions = GetPositions();

            foreach (Position position in positions)
            {
                if (position.TradeType == TradeType.Buy)
                    BuySide++;

                if (position.TradeType == TradeType.Sell)
                    SellSide++;
            }

            if (BuySide == positions.Length)
                Result = 0;

            if (SellSide == positions.Length)
                Result = 1;

            return Result;
        }

        private int GetDynamicPipstep(int CountOfBars, int division)
        {
            int Result;
            double HighestPrice = 0, LowestPrice = 0;
            int StartBar = MarketSeries.Close.Count - 2 - CountOfBars;
            int EndBar = MarketSeries.Close.Count - 2;

            for (int i = StartBar; i < EndBar; i++)
            {
                if (HighestPrice == 0 && LowestPrice == 0)
                {
                    HighestPrice = MarketSeries.High[i];
                    LowestPrice = MarketSeries.Low[i];
                    continue;
                }

                if (MarketSeries.High[i] > HighestPrice)
                    HighestPrice = MarketSeries.High[i];

                if (MarketSeries.Low[i] < LowestPrice)
                    LowestPrice = MarketSeries.Low[i];
            }

            Result = (int)((HighestPrice - LowestPrice) / Symbol.PipSize / division);

            return Result;
        }

        private double savedMaxBalance;
        private List<double> drawdown = new List<double>();
        private double GetMaxDrawdown()
        {
            savedMaxBalance = Math.Max(savedMaxBalance, Account.Balance);

            drawdown.Add((savedMaxBalance - Account.Balance) / savedMaxBalance * 100);
            drawdown.Sort();

            double maxDrawdown = drawdown[drawdown.Count - 1];

            return maxDrawdown;
        }

        private double FindLastPrice(TradeType tradeType)
        {
            double LastPrice = 0;

            foreach (Position position in GetPositions())
            {
                if (tradeType == TradeType.Buy)
                    if (position.TradeType == tradeType)
                    {
                        if (LastPrice == 0)
                        {
                            LastPrice = position.EntryPrice;
                            continue;
                        }
                        if (position.EntryPrice < LastPrice)
                            LastPrice = position.EntryPrice;
                    }

                if (tradeType == TradeType.Sell)
                    if (position.TradeType == tradeType)
                    {
                        if (LastPrice == 0)
                        {
                            LastPrice = position.EntryPrice;
                            continue;
                        }
                        if (position.EntryPrice > LastPrice)
                            LastPrice = position.EntryPrice;
                    }
            }

            return LastPrice;
        }

    }
}
