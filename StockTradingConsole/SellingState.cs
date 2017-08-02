﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StockTradingConsole
{
    enum SellingState
    {
        Initial = 0,
        Final, 
        InStrictUpLimit, // 一字板
        NotInStrictUpLimit, // 非一字板
        TriedToSellAtUpLimitPrice, // 涨停价挂牌
        Cancelling,
        ReadyForSelling, // 可以开卖
        SoldInContinuousBiddingPhase
    }
}