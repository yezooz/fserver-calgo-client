//#reference: ./libs/JsonFx.dll
//#reference: ./libs/EasyHttp.dll

using System;
using System.Collections.Generic;
using System.Net;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Requests;
using cAlgo.Indicators;
using EasyHttp.Http;

static class FServer
{
    public const string URL = "";
    public const string ACCOUNT_NO = "";
    public const int BETWEEN_REQUESTS = 15;
}

namespace cAlgo.Robots
{
    [Robot()]
    public class SIGNALREADER : Robot
    {
        private ServerSignal signal;
        private DateTime time;

        protected override void OnStart()
        {
            time = Server.Time;

            play();
        }

        protected override void OnTick()
        {
            TimeSpan diff = Server.Time - time;

            if (diff.TotalSeconds < FServer.BETWEEN_REQUESTS)
            {
                return;
            }

            play();

            foreach (Position position in Positions)
            {
                if (position.SymbolCode != Symbol.Code)
                {
                    continue;
                }

                new ServerTradeUpdate(position);
            }

            time = Server.Time;
        }

        protected override void OnStop()
        {

        }

        protected override void OnError(Error error)
        {
            switch (error.Code)
            {
                case ErrorCode.BadVolume:
                    Print("Bad Volume");
                    break;
                case ErrorCode.TechnicalError:
                    Print("Technical Error");
                    break;
                case ErrorCode.NoMoney:
                    Print("No Money");
                    break;
                case ErrorCode.Disconnected:
                    Print("Disconnected");
                    break;
                case ErrorCode.MarketClosed:
                    Print("Market Closed");
                    break;
            }
        }

        protected override void OnPositionOpened(Position position)
        {
            var trade = new ServerTrade(position, position.EntryPrice, Symbol.Spread);
            trade.openTrade();
        }

        protected override void OnPositionClosed(Position position)
        {
            var trade = new ServerTrade(position, Symbol.Bid, Symbol.Spread);
            trade.closeTrade();
        }

        protected override void OnPendingOrderCreated(PendingOrder newOrder)
        {

        }

        public void play()
        {
            fetchSignals();
            if (signal.signal_id != null)
            {
                openTrade();
            }
        }

        private void openTrade()
        {
            TradeType tradeType = TradeType.Buy;
            if (signal.direction == "SELL")
                tradeType = TradeType.Sell;

            var lot_size = (int)(signal.lot_size * 10);
            var request = new StopOrderRequest(tradeType, lot_size * 10000, signal.entry) 
            {
                Label = "signal_id=" + signal.signal_id,
                StopLoss = signal.sl,
                TakeProfit = signal.tp,
                Expiration = signal.expiration
            };
            Trade.Send(request);

            signal.signalStored();
        }

        private void fetchSignals()
        {
            try
            {
                var http = new HttpClient();
                http.Request.Accept = HttpContentTypes.ApplicationJson;
                var response = http.Get(FServer.URL + "/signal/reader/?broker=fxpro&account=" + FServer.ACCOUNT_NO + "&type=json&pair=" + Symbol.Code);
                signal = response.StaticBody<ServerSignal>();
            } catch (WebException e)
            {
                return;
            }
        }

        private void sendLog(string action, Position position)
        {
            try
            {
                var http = new HttpClient();
                http.Request.Accept = HttpContentTypes.ApplicationJson;
                var response = http.Get(FServer.URL + "/report/log/?broker=fxpro&account=" + FServer.ACCOUNT_NO + "&action=" + action + "&ticket_id=" + position.Id + "&profit=" + position.NetProfit);
            } catch (WebException e)
            {
                Print("Cannot connect to the server (sendLog)");
                return;
            }
        }
    }

    public class ServerSignal
    {
        public string pair { get; set; }
        public string signal_id { get; set; }
        public string direction { get; set; }
        public double tp { get; set; }
        public double sl { get; set; }
        public double entry { get; set; }
        public double lot_size { get; set; }
        public DateTime expiration { get; set; }
        public string comment { get; set; }

        public ServerSignal()
        {

        }

        private void sendLog(string action)
        {
            try
            {
                var http = new HttpClient();
                http.Request.Accept = HttpContentTypes.ApplicationJson;
                var response = http.Get(FServer.URL + "/report/log/?broker=fxpro&account=" + FServer.ACCOUNT_NO + "&action=" + action + "&signal_id=" + signal_id);
            } catch (WebException e)
            {
                return;
            }
        }

        public void signalStored()
        {
            sendLog("signal_stored");
        }

        public void signalActivated()
        {
            sendLog("signal_activated");
        }

        public void signalRemoved()
        {
            sendLog("signal_removed");
        }
    }

    public class ServerTrade
    {
        private Position pos;
        private double price;
        private double spread;

        public ServerTrade(Position _pos, double _price, double _spread)
        {
            pos = _pos;
            price = _price;
            spread = _spread;
        }

        public void openTrade()
        {
            string comment = pos.Label;
            if (comment.IndexOf("signal_id=") < 0)
                return;

            sendLog("open_trade", "signal_id=" + comment.Replace("signal_id=", "") + "&size=" + pos.Volume + "&projected_tp=" + pos.TakeProfit + "&projected_sl=" + pos.StopLoss + "&tp=" + pos.TakeProfit + "&sl=" + pos.StopLoss);
        }

        public void closeTrade()
        {
            sendLog("close_trade", "profit=" + pos.NetProfit + "&commision=" + pos.Commissions + "&swap=" + pos.Swap + "&net_profit=" + pos.NetProfit + "&gross_profit=" + pos.GrossProfit);
        }

        private void sendLog(string action, string extraParam = "")
        {
            try
            {
                var http = new HttpClient();
                http.Request.Accept = HttpContentTypes.ApplicationJson;
                var response = http.Get(FServer.URL + "/report/log/?broker=fxpro&account=" + FServer.ACCOUNT_NO + "&ticket=" + pos.Id + "&action=" + action + "&price=" + price + "&spread=" + spread + "&" + extraParam);
            } catch (WebException e)
            {
                return;
            }
        }
    }

    public class ServerTradeUpdate
    {
        public ServerTradeUpdate(Position pos)
        {
            try
            {
                var http = new HttpClient();
                http.Request.Accept = HttpContentTypes.ApplicationJson;
                var response = http.Get(FServer.URL + "/report/trade/?broker=fxpro&account=" + FServer.ACCOUNT_NO + "&ticket=" + pos.Id + "&commision=" + pos.Commissions + "&swap=" + pos.Swap + "&net_profit=" + pos.NetProfit + "&gross_profit=" + pos.GrossProfit);
            } catch (WebException e)
            {
                return;
            }
        }
    }
}
