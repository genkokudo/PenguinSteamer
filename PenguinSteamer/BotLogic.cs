using Chickenium;
using Chickenium.Dao;
using Microsoft.EntityFrameworkCore;
using Penguinium.Algorithm;
using Penguinium.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PenguinSteamer
{
    // TODO:static消そう

    /// <summary>
    /// BOTのメインロジック
    /// </summary>
    public class BotLogic
    {
        // TODO:あとで移動
        // 取引所ID(bitflyer)
        const int bitflyer = 1;
        // 取引所ID(bitflyerFX)
        const int bitflyerFX = 2;

        // データベース接続オプション
        private DbContextOptions dbContextOptions;

        // 現在のBOT全体の状態管理
        private BotStatusManager status;

        // レンジ用BOT(TODO:現在bitflyerFXのみだが、後で各取引所のディクショナリに変更)後で取引所とアルゴを組み合わせて管理する方法（ストラテジ）に変更すると思う
        static Dictionary<int, Algorithm> AlgorithmList;

        #region 初期化
        /// <summary>
        /// BOTのメインロジック
        /// Initializeを実行する
        /// </summary>
        /// <param name="dbContextOptions">データベース接続オプション</param>
        public BotLogic(DbContextOptions dbContextOptions)
        {
            // データベース接続オプション
            this.dbContextOptions = dbContextOptions;

            Initialize();
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public void Initialize()
        {
            // BOTステータス初期化
            status = new BotStatusManager(dbContextOptions);

            // 各種アルゴリズム初期化
            InitializeAlgorithm();

            // 試作品アルゴリズム初期化
            //var wip2 = new SmaTrapStrategy(bitflyer, status);
            //AlgorithmList.Add(1, wip2);
            //var wip2 = new TradeByVol(bitflyerFX, status);
            //AlgorithmList.Add(1, wip2);  // TODO:一時的にアルゴリズム4を割り当てる

            // データ取るのでコメントアウト
            ////AlgorithmList.Add(2, wip2);  // TODO:一時的にアルゴリズム2を割り当てる
            //var key1 = HistoryProperty.GetKey(HistoryKind.CHANNEL, 5, "15m");
            //var wip1 = new ChannelBreakOutStrategyNonStop(bitflyerFX, status, key1);
            //AlgorithmList.Add(1, wip1);  // TODO:一時的にアルゴリズム1を割り当てる
            //var key2 = HistoryProperty.GetKey(HistoryKind.CHANNEL, 6, "30m");
            //var wip2 = new ChannelBreakOutStrategyNonStop(bitflyerFX, status, key2);
            //AlgorithmList.Add(2, wip2);  // TODO:一時的にアルゴリズム2を割り当てる
            //var key3 = HistoryProperty.GetKey(HistoryKind.CHANNEL, 7, "30m");
            //var wip3 = new ChannelBreakOutStrategyNonStop(bitflyerFX, status, key3);
            //AlgorithmList.Add(3, wip3);  // TODO:一時的にアルゴリズム3を割り当てる
            //var key4 = HistoryProperty.GetKey(HistoryKind.CHANNEL, 8, "1h");
            //var wip4 = new ChannelBreakOutStrategyNonStop(bitflyerFX, status, key4);
            //AlgorithmList.Add(4, wip4);  // TODO:一時的にアルゴリズム4を割り当てる

            // 非同期なタスクを生成
            var task = Task.Run(async () =>
            {
                try
                {
                    Logger.Log("メインロジックを開始します");
                    await MainLogic();
                }
                catch (Exception ex)
                {
                    Logger.Log("Error: " + ex.Message);
                }
            });
            task.Wait();
        }
        #endregion

        #region アルゴリズム設定の初期化（アルゴリズムを追加したときは更新する必要あり）
        /// <summary>
        /// アルゴリズム設定の初期化
        /// </summary>
        public void InitializeAlgorithm()
        {
            AlgorithmList = new Dictionary<int, Algorithm>();

            // MAlgorithmから読み込み
            using (var dba = new MAlgorithmDbContext(dbContextOptions))
            {
                // データを取得
                var Algorithm = dba.MAlgorithm.Where(e => e.Enabled == 1);
                foreach (var itema in Algorithm)
                {
                    // テクニカルのパラメータを取得
                    var parameter = GetAlgorithmParameter(itema.Id);

                    // クラス作成
                    switch ((AlgorithmKind)itema.ClassId)
                    {
                        case AlgorithmKind.SMA_TRAP:
                            var temp1 = new SmaTrapStrategy(itema.ExchangeId, status, parameter);
                            AlgorithmList.Add(itema.Id, temp1);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 指定したIDのテクニカルのパラメータをDBから取得する
        /// </summary>
        /// <param name="algorithmId"></param>
        /// <returns></returns>
        private List<double> GetAlgorithmParameter(int algorithmId)
        {
            var result = new List<double>();
            using (var db = new MAlgorithmParameterDbContext(dbContextOptions))
            {
                // データを取得
                var AlgorithmParameter = db.MAlgorithmParameter.Where(e => e.AlgorithmId == algorithmId && e.Enabled == 1).OrderBy(e => e.Id);
                foreach (var item in AlgorithmParameter)
                {
                    result.Add(item.Value);
                }
            }
            return result;
        }
        #endregion

        #region 繰り返し実行するメインロジック

        // TODO:各取引所処理に対応させること（でも暫く動作はBFのみなので、とりあえず汎用対応できるように引数や更新タイミングを揃えること）
        async Task MainLogic()
        {
            Logger.Log("メインロジックを開始しました");
            while (true)
            {
                try
                {
                    // PubNubの分足を更新と、PubNubを使用する取引所の計算
                    foreach (var pubnubKey in status.PubnubList.Keys)   // 各取引所に対して実施
                    {
                        if (status.PubnubList[pubnubKey].ContainsKey((int)PubNubUse.Ticker))
                        {
                            // 取引所PubNubにTickerがあれば
                            var pubnubTicker = status.PubnubList[pubnubKey][(int)PubNubUse.Ticker];
                            pubnubTicker.ResetMinMax(status.StateList[bitflyer].State); // TODO:各取引所対応すること、今はBFのメンテに合わせる

                            // PubNubを使用する取引所はここで更新する
                            if (status.TechnicalList.ContainsKey(pubnubKey))
                            {
                                if (pubnubTicker.Ticker != null)
                                {
                                    if (status.StateList[pubnubKey].State == "CLOSED")
                                    {
                                        // メンテ中
                                    }
                                    else
                                    {
                                        // それぞれの取引所のクライアントとテクニカルを更新する
                                        status.Update(pubnubKey, pubnubTicker.Ticker);
                                    }
                                }
                                else
                                {
                                    Logger.Log("PubNub未取得 取引所ID:" + pubnubKey);
                                }
                            }
                        }
                    }

                    // 1分に1度の更新
                    minuteTime = (minuteTime + 1) % 60;  // 1分間に1回取得
                    if (minuteTime == 0)
                    {
                        await UpdateMinutesAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }

                // 売買処理
                foreach (var item in AlgorithmList.Keys)
                {
                    try
                    {
                        if (status.StateList[bitflyer].State == "CLOSED")
                        {
                            // メンテ中
                        }
                        else
                        {
                            // TODO:BFかどうか判定すること！
                            AlgorithmList[item].Update();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Error:BotLogic2 " + ex.Message);
                        Logger.Log("Error:BotLogic2 " + ex.StackTrace);
                    }
                }

                // 1秒待機
                await Task.Delay(1000);

            }
        }
        #endregion

        #region 1分に1回更新:1分ごとに呼び出すこと
        // 1分に1回更新、60までカウントしたら0に戻して処理を実行
        static int minuteTime = -1;

        /// <summary>
        /// 1分に1回更新
        /// </summary>
        async Task UpdateMinutesAsync()
        {
            // 取引所ステータスを取得（TODO: 今のところBFFXのみ）
            try
            {
                await UpdateState(bitflyer);
            }
            catch (Exception ex)
            {
                Logger.Log("Error:BotLogic UpdateState" + ex.Message);
            }
            //RUNNING: 通常稼働中
            //CLOSED: 取引停止中
            //STARTING: 再起動中
            //PREOPEN: 板寄せ中
            //CIRCUIT BREAK: サーキットブレイク発動中
            //AWAITING SQ: Lightning Futures の取引終了後 SQ（清算値）の確定前
            //MATURED: Lightning Futures の満期に到達

            if (status.StateList[bitflyer].State == "CLOSED")
            {
                Logger.Log("メンテ中" + bitflyer);
            }
            else
            {
                // 資産情報を取得（TODO: 今のところBFFXのみ）
                await UpdateCollateral(bitflyer);

                // 注文情報を取得（TODO: 今のところBFFXのみ）
                await UpdateOrders(bitflyer);

                // 建玉情報を取得（TODO: 今のところBFFXのみ）
                await UpdatePositions(bitflyer);
            }

            // TODO:PubNubを使用しない取引所の更新をする
            //foreach (var technical in averageList.Values)
            //{
            //    technical.Update(ticker);
            //    technicalList[pubnubKey].Update(pubnub.Ticker);
            //}

        }

        /// <summary>
        /// 資産情報を取得
        /// 取得失敗した場合nullにするので注意
        /// 現在の所BFのみ
        /// </summary>
        /// <param name="exchangeId">取引所ID</param>
        private async Task UpdateCollateral(int exchangeId)
        {
            // TODO:ビッフラのみ
            try
            {
                if (exchangeId == bitflyer)
                {
                    // 資産情報を取得
                    //Console.WriteLine("資産情報を取得");
                    status.CollateralList[bitflyer] = await status.Client[bitflyer].GetMyCollateral();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error:BotLogic UpdateCollateral" + ex.Message);
                status.CollateralList[bitflyer] = null;
            }
        }

        /// <summary>
        /// 注文情報を取得
        /// 取得失敗した場合nullにするので注意
        /// </summary>
        /// <param name="exchangeId">取引所ID</param>
        private async Task UpdateOrders(int exchangeId)
        {
            // TODO:BFのみ
            try
            {
                if (exchangeId == bitflyer)
                {
                    // 注文情報を取得
                    //Console.WriteLine("注文情報を取得");
                    status.ParentOrderList[bitflyer] = await status.Client[bitflyer].GetMyActiveParentOrders();
                    status.ChildOrderList[bitflyer] = await status.Client[bitflyer].GetMyActiveChildOrders();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error:BotLogic UpdateOrder" + ex.Message);
                status.ParentOrderList[bitflyer] = null;
                status.ChildOrderList[bitflyer] = null;
            }
        }

        /// <summary>
        /// 建玉情報を取得
        /// 取得失敗した場合nullにするので注意
        /// </summary>
        /// <param name="exchangeId">取引所ID</param>
        private async Task UpdatePositions(int exchangeId)
        {
            // TODO:BFのみ
            try
            {
                if (exchangeId == bitflyer)
                {
                    // 建玉情報を取得
                    //Console.WriteLine("建玉情報を取得");
                    status.PositionList[bitflyer] = await status.Client[bitflyer].GetMyPositions();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error:BotLogic UpdatePositions" + ex.Message);
                status.PositionList[bitflyer] = null;
            }
        }

        /// <summary>
        /// 取引所の状態を取得
        /// </summary>
        /// <param name="exchangeId">取引所ID</param>
        private async Task UpdateState(int exchangeId)
        {
            // TODO:BFのみ
            if (exchangeId == bitflyer)
            {
                // 取引所の状態を取得
                status.StateList[bitflyer] = await status.Client[bitflyer].GetBoardState();
            }
        }
        #endregion

        #region 現在の値段を取得（PubNubから取れなければAPIで取得）
        //// TODO:各取引所のTickerの取得方法をここに記述すること！
        ///// <summary>
        ///// 各取引所のTickerを取得
        ///// </summary>
        ///// <param name="exchangeId"></param>
        ///// <returns></returns>
        //private async Task<Ticker> GetTickerExchange(int exchangeId)
        //{
        //    Ticker ticker = null;

        //    if (ticker == null)
        //    {
        //        switch (exchangeId)
        //        {
        //            case bitflyer:
        //                ticker = await status.Client[bitflyer].GetTicker();
        //                break;
        //            default:
        //                break;
        //        }
        //    }
        //    return ticker;
        //}

        // TickerからCandleへの変換は、CandleクラスのUpdateByTickerで行うが、
        // PubNub非対応取引所はAPIを一定時間ごとに叩くので、それよりも1段階上のタイムスケールでろうそくを作る。
        // どこかで各取引所のろうそくを溜めておく必要がある。

        // 結局、PubNub非対応取引所の一番短いタイムスケール以外をろうそく表示するには、そのろうそく達を保持する必要がある。
        // 最終的には、DBでろうそくテーブルを作成して一括処理するのがスマートな実装。・・・と思ったが、実時間より1単位分計算が遅くなるので、最新の各ろうそくはBOT保持（このろうそくのDB書き込みはしない）。一括処理は表示用に行う。

        // 売買アルゴにろうそくは使用しないので、実装は後回し。
        #endregion
    }

    #region AlgorithmKind:アルゴリズムの種類
    /// <summary>
    /// リストの種類
    /// </summary>
    public enum AlgorithmKind
    {
        /// <summary>
        /// SMA参照トラリピ
        /// </summary>
        SMA_TRAP = 0
    }
    #endregion
}
