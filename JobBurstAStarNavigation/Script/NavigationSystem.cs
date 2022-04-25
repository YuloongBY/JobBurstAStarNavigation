/**
 * The MIT License (MIT)
 *
 * Copyright (c) 2022 YuloongBY - Github: github.com/YuloongBY
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of
 * this software and associated documentation files (the "Software"), to deal in
 * the Software without restriction, including without limitation the rights to
 * use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
 * the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
 * FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
 * CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

//#define USING_BURST
//#define NAVIGATION_LOG

using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
#if USING_BURST
using Unity.Burst;
#endif

/// <summary>
/// ナビゲーションシステム
/// </summary>
public class NavigationSystem : MonoBehaviour
{
    /// <summary>
    /// ナビゲーション完成した時のコールバック
    /// </summary>
    /// <param name="_route">経路</param>
    /// <param name="_result">結果</param>
    /// <param name="_gridID">グリッドID</param>
    /// <param name="_isSameLast">前回と同じかどうか</param>
    public delegate void NAVIGATION_FINISH_CALLBACK( List<Vector2Int> _route , NAVIGATION_RESULT _result , int _gridID , bool _isSameLast );

    /// <summary>
    /// 結果
    /// </summary>
    public enum NAVIGATION_RESULT
    {
        SUCCESS = 0,
        DEADEND    ,     
        FAILD      ,
        NONE       ,
    }

    /// <summary>
    /// 経路タイプ
    /// </summary>
    public enum NAVIGATION_ROUTE_TYPE
    {
        FOUR_DIRECTION = 0 ,  //4 direction
        EIGHT_DIRECTION    ,  //8 direction
        FREE_DIRECTION     ,  //free direction
    }

    //フリータスク
    private List<NavigationTask> freeTask_;
    //登録タスク
    private Dictionary<MonoBehaviour,NavigationTask> registerTask_;
    //グリッドスレッド
    private Dictionary<int,GridThread> gridThread_;

    /// <summary>
    /// 開始
    /// </summary>
    void Start(){}
        
    /// <summary>
    /// 更新
    /// </summary>
    void Update()
    {
        ThreadUpdate();
    }

    /// <summary>
    /// 破棄
    /// </summary>
    void OnDestroy()
    {
        AllClear_Imp();
    }

    /// <summary>
    /// スレッド更新
    /// </summary>
    private void ThreadUpdate()
    {
        //スレッド更新
        foreach( var thread in gridThread_ )
        {
            thread.Value.ThreadUpdate( thread.Key );            
        }

        //すべて終了したかどうかを判断
        bool isFinish = true;
        foreach( var thread in gridThread_ )
        {
            if( !thread.Value.IsNavigationFinish())
            {
                isFinish = false;
                break;
            }
        }

        if( isFinish )
        {
            #if NAVIGATION_LOG
            Debug.Log( "すべてのスレッド実行完了" );
            #endif
            enabled = false;
        }
    }

    /// <summary>
    /// すべてクリア(実行)
    /// </summary>
    private void AllClear_Imp()
    {
        //スレッドをクリア
        foreach( var thread in gridThread_ )
        {
            thread.Value.Clear();
        }
        gridThread_.Clear();

        //登録タスクをクリア
        foreach( var task in registerTask_ )
        {
            task.Value.Clear();
        }
        registerTask_.Clear();
        
        //フリータスクをクリア         
        freeTask_.Clear(); 
    }

    /// <summary>
    /// ユニット情報を更新(実行)
    /// </summary>
    private void ReplaceUnitInfo_Imp( Vector2Int _unitIdx , UnitInfo _unitInfo , int _gridID )
    {
        if( GetGridThread( _gridID , out var thread ))
        {
            thread.ReplaceUnitInfo( _unitIdx , _unitInfo );
        }
    }

    /// <summary>
    /// グリッド情報を更新(実行)
    /// </summary>
    private void ReplaceGridInfo_Imp( UnitInfo[,] _gridInfo , int _gridID )
    {
        if( GetGridThread( _gridID , out var thread ))
        {
            thread.ReplaceGridInfo( _gridInfo );
        }
    }

    /// <summary>
    /// グリッドスレッドを取得
    /// </summary>
    private bool GetGridThread( int _gridID , out GridThread thread )
    {
        if( gridThread_.TryGetValue( _gridID , out thread ))
        {
            return true;
        }        
        Debug.LogError( $"グリッド見つけてない({_gridID}) , [ImportGrid]処理でグリッドを導入してください。" );
        return false;
    }

    /// <summary>
    /// グリッドを導入（実行）
    /// </summary>
    private void ImportGrid_Imp( UnitInfo[,] _gridInfo , int _gridID , int _threadNum )
    {   
        bool isSuccess = gridThread_.TryGetValue( _gridID , out var thread );
        if( !isSuccess )
        {
            thread = new GridThread();
            gridThread_.Add( _gridID , thread );
        }

        thread.ImportGrid( _gridInfo , _threadNum );        
    }

    /// <summary>
    /// 登録(実行)
    /// </summary>
    private void Register_Imp( MonoBehaviour _register , Vector2Int _registerSize , NAVIGATION_ROUTE_TYPE _routeType , NAVIGATION_FINISH_CALLBACK _finishCallback , params int[] _blockType )
    {
        if( !registerTask_.ContainsKey( _register ))
        {
            NavigationTask task = null;
            if( freeTask_.Count > 0 )
            { 
                task = freeTask_[ 0 ];
                freeTask_.RemoveAt( 0 );
            }
            else
            {
                task = new NavigationTask();
            }
            task.Init( _register , _registerSize , _routeType , _finishCallback , _blockType );
            registerTask_.Add( _register , task );
        }
        else
        {
            Debug.LogWarning( $"既に登録した({_register.name})" );
        }
    }

    /// <summary>
    /// 登録を解除(実行) 
    /// </summary>
    private void Unregister_Imp( MonoBehaviour _register )
    {
        if( registerTask_.TryGetValue( _register , out var task ))
        {
            //スレッドは実行している場合、強制的に終了させる
            task.ForceCompleted();
            task.Clear();
            freeTask_.Add( task );
            registerTask_.Remove( _register );
        }
    }   

    /// <summary>
    /// サイズを設定(実行)
    /// </summary>
    private void SetSize_Imp( MonoBehaviour _register , Vector2Int _registerSize )
    {
        if( registerTask_.TryGetValue( _register , out var task ))
        {
            task.SetSize( _registerSize );
        }
    }

    /// <summary>
    /// 経路タイプを設定(実行)
    /// </summary>
    private void SetRouteType_Imp( MonoBehaviour _register , NAVIGATION_ROUTE_TYPE _routeType )
    {
        if( registerTask_.TryGetValue( _register , out var task ))
        {
            task.SetRouteType( _routeType );
        }
    }

    /// <summary>
    /// ブロックタイプを設定(実行)
    /// </summary>
    private void SetBlockType_Imp( MonoBehaviour _register , params int[] _blockType )
    {
        if( registerTask_.TryGetValue( _register , out var task ))
        {
            task.SetBlockType( _blockType );
        }
    }

    /// <summary>
    /// ナビゲーション開始(実行)
    /// </summary>
    private void NavigationStart_Imp( MonoBehaviour _register , Vector2Int _startIdx , Vector2Int _endIdx , int _gridID , NAVIGATION_FINISH_CALLBACK _temFinishCallback )
    {
        if( registerTask_.TryGetValue( _register , out var task ))
        {
            if( gridThread_ != null && gridThread_.TryGetValue( _gridID , out GridThread thread ))
            {
                bool isSuccess = thread.NavigationStart( task , _startIdx , _endIdx , _gridID , _temFinishCallback );
                if( isSuccess )
                {
                    enabled = true;
                }
            }
            else
            {
                Debug.LogError( $"グリッド見つけてない({_gridID}) , [ImportGrid]処理でグリッドを導入してください。" );
            }          
        }
        else
        {
            Debug.LogWarning( $"まだ登録してない({_register.name})" );
        }
    }

    /// <summary>
    /// インスタンスを取得
    /// </summary> 
    private static NavigationSystem Instance_
    {
        get
        {
#if NAVIGATION_LOG
            if( instance_ == null )
            {
                Debug.LogWarning( "NavigationSystem is not found" );
            }
#endif
            return instance_;
        }
    }
    private static NavigationSystem instance_ = null;

    /// <summary>
    /// ユニット情報を差し替え
    /// ※グリッドサイズ変わらず、グリッド中のユニット情報だけ変更したい場合、利用してください。
    /// </summary>
    /// <param name="_unitIdx">ユニットインデックス</param>
    /// <param name="_unitInfo">ユニット情報</param>
    /// <param name="_gridID">グリッドID(マルチグリッドの場合だけ設定必要)</param>
    public static void ReplaceUnitInfo( Vector2Int _unitIdx , UnitInfo _unitInfo , int _gridID = 0 )
    {
        if( Instance_ == null ) return;
        Instance_.ReplaceUnitInfo_Imp( _unitIdx , _unitInfo , _gridID ); 
    }

    /// <summary>
    /// グリッド全体を差し替え
    /// ※グリッドが変更したい場合、利用する
    /// ※メモリ割り当て処理発生するので、注意してください。
    /// </summary>
    /// <param name="_gridInfo">グリッド情報</param>
    /// <param name="_gridID">グリッドID(マルチグリッドの場合だけ設定必要)</param>
    public static void ReplaceGridInfo( UnitInfo[,] _gridInfo , int _gridID = 0 )
    {
        if( Instance_ == null ) return;
        Instance_.ReplaceGridInfo_Imp( _gridInfo , _gridID );  
    }

    /// <summary>
    /// グリッドを導入
    /// ※メモリ割り当て処理発生するので、注意してください。
    /// </summary>
    /// <param name="_gridInfo">グリッド情報</param>
    /// <param name="_gridID">グリッドID(マルチグリッドの場合だけ設定必要)</param>
    /// <param name="_threadNum">同時発生最大スレッド数</param>
    public static void ImportGrid( UnitInfo[,] _gridInfo , int _gridID = 0 , int _threadNum = 5 )
    {
        if( Instance_ == null ) return;
        Instance_.ImportGrid_Imp( _gridInfo , _gridID , _threadNum );       
    }

    /// <summary>
    /// 登録
    /// ※ナビゲーション機能を利用する前に、利用者がこの処理でナビゲーションシステムに登録してください。
    /// </summary>
    /// <param name="_register">登録者</param>
    /// <param name="_registerSize">登録者サイズ</param>
    /// <param name="_routeType">経路タイプ</param>    
    /// <param name="_finishCallback">完成した時のコールバック</param>
    /// <param name="_blockType">障害物タイプ</param>
    public static void Register( MonoBehaviour _register , Vector2Int _registerSize , NAVIGATION_ROUTE_TYPE _routeType , NAVIGATION_FINISH_CALLBACK _finishCallback , params int[] _blockType )
    {
        if( Instance_ == null ) return;
        Instance_.Register_Imp( _register , _registerSize , _routeType , _finishCallback , _blockType );
    }

    /// <summary>
    /// 登録解除
    /// ※利用者を破棄、ナビゲーション機能を使わなくなる場合、この処理でナビゲーションシステムから登録を解除してください。
    /// </summary>
    /// <param name="_register">登録者</param>
    public static void Unregister( MonoBehaviour _register )
    {
        if( Instance_ == null ) return;
        Instance_.Unregister_Imp( _register );
    }

    /// <summary>
    /// 登録者のサイズを設定
    /// </summary>
    /// <param name="_register">登録者</param>
    /// <param name="_registerSize">登録者サイズ</param>
    public static void SetSize( MonoBehaviour _register , Vector2Int _registerSize )
    {
        if( Instance_ == null ) return;
        Instance_.SetSize_Imp( _register , _registerSize );
    }

    /// <summary>
    /// 登録者の経路タイプを設定
    /// </summary>
    /// <param name="_register">登録者</param>
    /// <param name="_routeType">経路タイプ</param>
    public static void SetRouteType( MonoBehaviour _register , NAVIGATION_ROUTE_TYPE _routeType )
    {
        if( Instance_ == null ) return;
        Instance_.SetRouteType_Imp( _register , _routeType );
    }

    /// <summary>
    /// 登録者のブロックタイプを設定
    /// </summary>
    /// <param name="_register">登録者</param>
    /// <param name="_blockType">ブロックタイプ</param>
    public static void SetBlockType( MonoBehaviour _register , params int[] _blockType )
    {
        if( Instance_ == null ) return;
        Instance_.SetBlockType_Imp( _register , _blockType );
    }

    /// <summary>
    /// ナビゲーション開始
    /// </summary>
    /// <param name="_register">登録者</param>
    /// <param name="_startIdx">開始インデックス</param>
    /// <param name="_endIdx">終了インデックス</param>
    /// <param name="_gridID">グリッドID(マルチグリッドの場合だけ設定必要)</param>
    /// <param name="_temFinishCallback">完了した時の一時コールバック</param>
    public static void NavigationStart( MonoBehaviour _register , Vector2Int _startIdx , Vector2Int _endIdx , int _gridID = 0 , NAVIGATION_FINISH_CALLBACK _temFinishCallback = null )
    {
        if( Instance_ == null ) return;
        Instance_.NavigationStart_Imp( _register , _startIdx , _endIdx , _gridID , _temFinishCallback );
    }

    /// <summary>
    /// ナビゲーション情報をすべて削除
    /// ※ステージ遷移の場合、利用することが多い
    /// </summary>
    public static void AllClear()
    {
        if( Instance_ == null ) return;
        Instance_.AllClear_Imp();
    }

    /// <summary>
    /// 強制破棄
    /// </summary>
    public static void ForceDestroy()
    {
        if (instance_ != null)
        {
            Destroy( instance_.gameObject );
            instance_ = null;
        }
    }

    /// <summary>
    /// 生成
    /// ※シングルトンなので、最初一回だけ呼べば良い
    /// </summary>
    public static void Create()
    {
        if( instance_ == null )
        {
            var go = new GameObject( "NavigationSystem" );
            DontDestroyOnLoad( go );
            instance_ = go.AddComponent<NavigationSystem>();     
            instance_.gridThread_     = new Dictionary<int, GridThread>();
            instance_.freeTask_       = new List<NavigationTask>();
            instance_.registerTask_   = new Dictionary<MonoBehaviour, NavigationTask>();
            instance_.enabled = false;
        }
    }

    /// <summary>
    /// ユニット情報
    /// </summary>
    public struct UnitInfo
    {
        //タイプ
        public int unitType_;
        //ウエイト
        public int unitWeight_;       

        public UnitInfo( int _unitType )
        {
            unitType_    = _unitType;
            unitWeight_  = 0;
        }

        /// <summary>
        /// クリア
        /// </summary>
        public void Clear()
        {
            unitType_    = 0;
            unitWeight_  = 0;
        }
    }

    /// <summary>
    /// ナビゲーションダイナミック情報
    /// </summary>
    private struct NavigationDynamicInfo
    {
        public Vector2Int  startIdx_;           //開始インデックス
        public Vector2Int  endIdx_;             //終了インデックス
        public Vector2Int  size_;               //サイズ
        public NAVIGATION_ROUTE_TYPE routeType_;  //経路タイプ
                        
        /// <summary>
        /// リセット
        /// </summary>
        public static NavigationDynamicInfo Reset()
        {
            NavigationDynamicInfo info;
            info.startIdx_   = Vector2Int.zero;
            info.endIdx_     = Vector2Int.zero;
            info.size_       = Vector2Int.one;
            info.routeType_  = NAVIGATION_ROUTE_TYPE.FREE_DIRECTION;
            return info;
        }

        /// <summary>
        /// 同じかどうか判断
        /// </summary>
        public static bool IsSame( NavigationDynamicInfo _a , NavigationDynamicInfo _b )
        {
            if( _a.startIdx_  != _b.startIdx_  ) return false;
            if( _a.endIdx_    != _b.endIdx_    ) return false;
            if( _a.size_      != _b.size_      ) return false;
            if( _a.routeType_ != _b.routeType_ ) return false;
            return true;
        }
    }

    /// <summary>
    /// グリッドスレッド
    /// </summary>
    private class GridThread
    {
        //スレッドパッケージ
        private NavigationThreadPackage[] threadPackage_;

        //利用中スレッドインデックス
        private List<int> usingThreadIdx_;
        
        //タスクキュー
        private List<NavigationTask> taskQueue_;

        //グリッド情報配列
        public NativeArray<UnitInfo> gridInfo_;

        //グリッド行数
        private int rowNum_;
        
        //グリッド列数
        private int colNum_;

        //リセット用配列
        private int[] resetArray_;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public GridThread()
        {
            threadPackage_  = null;
            usingThreadIdx_ = new List<int>();
            taskQueue_      = new List<NavigationTask>();
            rowNum_         = -1;
            colNum_         = -1;
            resetArray_     = null;
        }

        /// <summary>
        /// スレッド更新
        /// </summary>
        public void ThreadUpdate( int _gridID )
        {
            for( int nCnt = usingThreadIdx_.Count - 1 ; nCnt >= 0 ; nCnt -- )
            {
                int threadIdx = usingThreadIdx_[ nCnt ];
                var threadPkg = threadPackage_[ threadIdx ];

                if( threadPkg.IsCompleted())
                {
                    //結果を分析
                    var result = threadPkg.AnalyzeThreadResult( _gridID );
                    switch( result )
                    {
                        //成功
                        case NavigationThreadPackage.THREAD_RESULT.SUCCESS:
                        {
                            #if NAVIGATION_LOG
                            Debug.Log( "ナビゲーション成功" );
                            #endif

                            //タスクを一時保存
                            var task = threadPkg.GetTask();
                            //タスクをクリア
                            threadPkg.ClearTask();
                            //待機タスクをスレッドスケジュールに代入
                            bool isSuccess = InThreadSchedueByTaskList( threadIdx );
                            if( !isSuccess )
                            {
                                //すべてのタスクを完成したと、利用中のスレッドインデックス配列から削除
                                RemoveAtUsingThreadIdx( threadIdx );
                            }
                            //結果処理
                            threadPkg.NavigationFinish( task );
                        }
                        break;
                        //タスクがキャンセルされ
                        case NavigationThreadPackage.THREAD_RESULT.TASK_CANCEL:
                        {
                            #if NAVIGATION_LOG
                            Debug.Log( "タスクがキャンセルされ，もう一度計算" );
                            #endif 
                            //再計算
                            threadPkg.ReInSchedue( resetArray_ );
                        }
                        break;
                        default:
                        {
                            #if NAVIGATION_LOG
                            Debug.Log( $"結果が無効({result})" );
                            #endif
                            //タスクをクリア
                            threadPkg.ClearTask();
                            //待機タスクをスレッドスケジュールに代入
                            bool isSuccess = InThreadSchedueByTaskList( threadIdx );
                            if( !isSuccess )
                            {
                                //すべてのタスクを完成したと、利用中のスレッドインデックス配列から削除
                                RemoveAtUsingThreadIdx( threadIdx );
                            }
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// このスレッドのナビゲーション処理を完了したかどうか
        /// </summary>
        public bool IsNavigationFinish()
        {
            if( taskQueue_.Count > 0 ) return false;
            if( usingThreadIdx_.Count > 0 ) return false;  
            return true;
        }

        /// <summary>
        /// 待機タスクをスレッドスケジュールに代入
        /// </summary>
        private bool InThreadSchedueByTaskList( int _threadIdx )
        {
            if( taskQueue_.Count > 0 )
            {
                if( _threadIdx >= 0 && _threadIdx < threadPackage_.Length )
                {
                    AddToUsingThreadIdx( _threadIdx );                
                    threadPackage_[ _threadIdx ].InSchedue( taskQueue_[ 0 ] , resetArray_ );            
                    taskQueue_.RemoveAt( 0 );
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 利用中のスレッドインデックス配列に追加
        /// </summary>
        private void AddToUsingThreadIdx( int _threadIdx )
        {
            if( !usingThreadIdx_.Contains( _threadIdx ))
            {
                usingThreadIdx_.Add( _threadIdx );
            }
        }

        /// <summary>
        /// 利用中のスレッドインデックス配列から削除
        /// </summary>
        private void RemoveAtUsingThreadIdx( int _threadIdx )
        {
            if( usingThreadIdx_.Contains( _threadIdx ))
            {
                usingThreadIdx_.Remove( _threadIdx );
            }
        }

        /// <summary>
        /// クリア
        /// </summary>
        public void Clear()
        {
            //スレッドクリア
            if( threadPackage_ != null )
            {
                int threadNum = threadPackage_.Length;
                for( int nCnt = 0 ; nCnt < threadNum ; nCnt++ )
                {
                    if( threadPackage_[ nCnt ] != null )
                    {
                        threadPackage_[ nCnt ].Uninit();
                        threadPackage_[ nCnt ] = null;
                    }
                }
                threadPackage_ = null;
            }

            gridInfo_.SafeDispose();

            //利用中スレッドインデックスをクリア
            usingThreadIdx_.Clear();

            //待機タスクをクリア
            foreach( var task in taskQueue_ )
            {
                task.ClearLastInfo();
            }  
            taskQueue_.Clear();
        }

        /// <summary>
        /// 待機スレッドインデックスを取得
        /// </summary>
        private int GetIdleThreadIdx()
        {
            for( int nCnt = 0 ; nCnt < threadPackage_.Length ; nCnt ++ )
            {
                if( threadPackage_[ nCnt ].IsIdle())
                {
                    return nCnt;
                }
            }
            return -1;
        }

        /// <summary>
        /// ユニット情報を更新
        /// </summary>
        public void ReplaceUnitInfo( Vector2Int _unitIdx , UnitInfo _unitInfo )
        {
            foreach( var pkg in threadPackage_ )
            {
                pkg.ForceCompleted();
            }

            int idx = _unitIdx.y * colNum_ + _unitIdx.x;
            if( idx >= 0 && idx <= gridInfo_.Length )
            {
                gridInfo_[ idx ] = _unitInfo;
            }
        }

        /// <summary>
        /// グリッド情報を更新
        /// </summary>
        public void ReplaceGridInfo( UnitInfo[,] _gridInfo )
        {
            if( _gridInfo.GetLength(0) != rowNum_ ||
                _gridInfo.GetLength(1) != colNum_ )
            {
                Debug.LogError( "行列数違うグリッドが差し替えできません、[ImportGrid]処理でもう一度グリッドを導入してください。" );
                return;
            }

            foreach( var pkg in threadPackage_ )
            {
                pkg.ForceCompleted();
            }

            for( int nRow = 0 , nCnt = 0 ; nRow < rowNum_ ; nRow ++ )
            {
                for( int nCol = 0; nCol < colNum_ ; nCol ++ , nCnt ++ )
                { 
                    gridInfo_[ nCnt ] = _gridInfo[ nRow , nCol ];
                }
            }
        }

        /// <summary>
        /// グリッドを導入
        /// </summary>
        public void ImportGrid( UnitInfo[,] _gridInfo , int _threadNum )
        {
            rowNum_ = _gridInfo.GetLength( 0 );
            colNum_ = _gridInfo.GetLength( 1 );

            Clear();
        
            gridInfo_ = new NativeArray<UnitInfo>( rowNum_ * colNum_ , Allocator.Persistent );
            resetArray_ = new int[ rowNum_ * colNum_ ];
        
            for( int nRow = 0 , nCnt = 0 ; nRow < rowNum_ ; nRow ++ )
            {
                for( int nCol = 0; nCol < colNum_ ; nCol ++ , nCnt ++ )
                { 
                    gridInfo_[ nCnt ] = _gridInfo[ nRow , nCol ];
                    resetArray_[ nCnt ] = -1;
                }
            }

            //スレッド初期化
            threadPackage_ = new NavigationThreadPackage[ _threadNum ];
            for( int nCnt = 0 ; nCnt < _threadNum ; nCnt++ )
            {
                threadPackage_[ nCnt ] = new NavigationThreadPackage( rowNum_ , colNum_ , gridInfo_ );
            }
        }

        /// <summary>
        /// ナビゲーション処理開始
        /// </summary>
        public bool NavigationStart( NavigationTask _task , Vector2Int _startIdx , Vector2Int _endIdx , int _gridID , NAVIGATION_FINISH_CALLBACK _temFinishCallback )
        {
            bool isInTaskQueue = _task.TryTaskStart(  _startIdx , _endIdx , _gridID , _temFinishCallback );
            if( isInTaskQueue )
            {
                //既にキュー中に存在
                if( taskQueue_.Contains( _task ))
                {
                    return false;
                }
                taskQueue_.Add( _task );

                //待機タスクをスレッドスケジュールに代入
                InThreadSchedueByTaskList( GetIdleThreadIdx());
                return true;
            }           
            return false;
        }
    }

    /// <summary>
    /// スレッドパッケージ
    /// </summary>
    private class NavigationThreadPackage
    {
        /// <summary>
        /// スレッドの実行結果
        /// </summary>
        public enum THREAD_RESULT
        {
            SUCCESS = 0 ,   //成功
            TASK_NONE   ,   //タスクが存在しない
            TASK_CANCEL ,   //タスクがキャンセルされ
            TASK_CHANGED ,  //タスク内容の変化があり
            GRID_CHANGED ,  //グリッドIDの変化があり
            SAME_LAST    ,  //前回の結果と同じ
        }

        //スレッド
        private NaviGationThreadClass thread_ = null;

        //タスク
        private NavigationTask task_ = null;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public NavigationThreadPackage( int _rowNum , int _colNum , NativeArray<UnitInfo> _gridInfo )
        {
            thread_ = new NaviGationThreadClass( _rowNum , _colNum , _gridInfo );
            task_   = null;
        }

        /// <summary>
        /// 終了
        /// </summary>
        public void Uninit()
        {
            if( thread_ != null )
            {
                thread_.Struct_.Uninit();
                thread_ = null;
            }
            ClearTask();
        }

        /// <summary>
        /// スレッドを強制中止
        /// </summary>
        public void ForceCompleted()
        {
            thread_.Struct_.ForceCompleted();
            if( task_ != null ) task_.SetUsingThread( null );            
        }

        /// <summary>
        /// 待機？
        /// </summary>
        public bool IsIdle()
        {
            return !thread_.Struct_.IsExecuting() && task_ == null;
        }

        /// <summary>
        /// スレッド完了チェック
        /// </summary>
        public bool IsCompleted()
        {
            return thread_.Struct_.IsCompleted();
        }

        /// <summary>
        /// 結果処理
        /// </summary>
        public void NavigationFinish( NavigationTask _task )
        {
            if( _task != null )
            {
                _task.NavigationFinish( thread_.Struct_.GetResultInfo() , thread_.Struct_.GetRoute());        
            }
        }

        /// <summary>
        /// スレッドスケジュールに代入
        /// </summary>
        public void InSchedue( NavigationTask _task , int[] _resetArray )
        {
            task_ = _task;
            task_.SetUsingThread( thread_ );
            thread_.Struct_.InSchedue( _task , _resetArray );
        }

        /// <summary>
        /// 持ってるタスクをもう一度スケジュールに代入し、再計算
        /// </summary>
        public void ReInSchedue( int[] _resetArray )
        {
            InSchedue( task_ , _resetArray );
        }

        /// <summary>
        /// スレッドの実行結果を分析
        /// </summary>
        public THREAD_RESULT AnalyzeThreadResult( int _gridID )
        {
            //タスクや登録者がない場合
            if( task_ == null || !task_.HaveRegister()) return THREAD_RESULT.TASK_NONE;

            //利用されてるスレッドはこのスレッドではない場合
            if( !task_.IsCheckUsingThread( thread_ )) return THREAD_RESULT.TASK_CANCEL;

            //タスク内容の変化があり           
            if( !task_.IsSameAsThread( thread_ )) return THREAD_RESULT.TASK_CHANGED;

            //グリッドIDの変化があり
            if( !task_.IsSameGridID( _gridID )) return THREAD_RESULT.GRID_CHANGED;

            //前回の情報と同じ
            if( task_.IsSameAsLast( thread_.Struct_.GetDynamicInfo()))
            {
                return THREAD_RESULT.SAME_LAST;
            }

            return THREAD_RESULT.SUCCESS;
        }

        /// <summary>
        /// タスクをクリア
        /// </summary>
        public void ClearTask()
        {
            if( task_ != null )
            {
                task_.SetUsingThread( null );                
                task_ = null;                
            }
        }

        /// <summary>
        /// タスクを取得
        /// </summary>
        public NavigationTask GetTask()
        {
            return task_;
        }
    }

    /// <summary>
    /// ナビゲーションタスク
    /// </summary>
    private class NavigationTask
    {
        //登録者
        private MonoBehaviour register_ = null;
        //ナビゲーションで計算した経路
        private List<Vector2Int> navigationRoute_ = null;
        //ナビゲーションで計算した結果
        private NAVIGATION_RESULT navigationResult_ = NAVIGATION_RESULT.NONE;
        //スレッド完了した時のコールバック
        private NAVIGATION_FINISH_CALLBACK finishCallback_ = null;        
        //スレッド完了した時の一時コールバック
        private NAVIGATION_FINISH_CALLBACK temFinishCallback_ = null;        
        //障害物タイプ配列
        private NativeArray<int> blockType_;
        //利用されてるスレッド
        private NaviGationThreadClass usingThread_ = null;
        //ダイナミック情報
        private NavigationDynamicInfo dynamicInfo_;
        //グリッドID
        private int gridID_ = -1;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public NavigationTask()
        {
            navigationRoute_ = new List<Vector2Int>();
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~NavigationTask(){}

        /// <summary>
        /// クリア
        /// </summary>
        public void Clear()
        {
            usingThread_       = null;
            register_          = null;
            finishCallback_    = null;      
            temFinishCallback_ = null;
            dynamicInfo_       = NavigationDynamicInfo.Reset();
            navigationResult_    = NAVIGATION_RESULT.NONE;
            navigationRoute_.Clear();            
            blockType_.SafeDispose();
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public void Init( MonoBehaviour _register , Vector2Int _registerSize , NAVIGATION_ROUTE_TYPE _routeType , NAVIGATION_FINISH_CALLBACK _finishCallback , params int[] _blockType )
        {
            register_       = _register;
            finishCallback_ = _finishCallback;

            dynamicInfo_            =  NavigationDynamicInfo.Reset();
            dynamicInfo_.size_      = _registerSize;
            dynamicInfo_.routeType_ = _routeType;
            
            if( _blockType != null && _blockType.Length > 0 )
            {
                blockType_ = new NativeArray<int>( _blockType.Length , Allocator.Persistent );
                blockType_.CopyFrom( _blockType );
            }
        }

        /// <summary>
        /// サイズを設定
        /// </summary>
        public void SetSize( Vector2Int _size )
        {
            ClearLastInfo();
            dynamicInfo_.size_ = _size;
        }

        /// <summary>
        /// 経路タイプを設定
        /// </summary>
        public void SetRouteType( NAVIGATION_ROUTE_TYPE _routeType )
        {
            ClearLastInfo();
            dynamicInfo_.routeType_ = _routeType;
        }
        
        /// <summary>
        /// ブロックタイプを設定
        /// </summary>
        public void SetBlockType( params int[] _blockType )
        {
            ClearLastInfo();
            
            //スレッド実行中の場合、強制終了
            if( usingThread_ != null )
            {
                usingThread_.Struct_.ForceCompleted();
                usingThread_ = null;
            }

            blockType_.SafeDispose();

            if( _blockType != null && _blockType.Length > 0 )
            {
                blockType_ = new NativeArray<int>( _blockType.Length , Allocator.Persistent );
                blockType_.CopyFrom( _blockType );
            }
        }

        /// <summary>
        /// 登録者が存在するかどうか判断
        /// </summary>
        public bool HaveRegister()
        { 
            return register_ != null; 
        }

        /// <summary>
        /// 障害物タイプを取得 
        /// </summary>
        public NativeArray<int> GetBlockType(){ return blockType_;}

        /// <summary>
        /// ダイナミック情報を取得
        /// </summary>
        public NavigationDynamicInfo GetDynamicInfo(){ return dynamicInfo_;}

        /// <summary>
        /// スレッドを設定
        /// </summary>
        public void SetUsingThread( NaviGationThreadClass _usingThread )
        {
            usingThread_ = _usingThread;
        }

        /// <summary>
        /// スレッドを判断
        /// </summary>
        public bool IsCheckUsingThread( NaviGationThreadClass _checkThread )
        {
            return usingThread_ == _checkThread;
        }

        /// <summary>
        /// スレッドを強制終了
        /// </summary>
        public void ForceCompleted()
        {
            if( usingThread_ != null )
            { 
                usingThread_.Struct_.ForceCompleted();
            }
        }
        
        /// <summary>
        /// タスク開始
        /// </summary>
        public bool TryTaskStart( Vector2Int _startIdx , Vector2Int _endIdx , int _gridID , NAVIGATION_FINISH_CALLBACK _temFinishCallback = null )
        {   
            temFinishCallback_ = _temFinishCallback;

            //前回の情報と同じかどうか判断
            NavigationDynamicInfo checkInfo;
            {
                checkInfo.size_      = dynamicInfo_.size_;
                checkInfo.routeType_ = dynamicInfo_.routeType_;
                checkInfo.startIdx_  = _startIdx;
                checkInfo.endIdx_    = _endIdx;
            }

            if( IsSameAsLast( checkInfo ) && IsSameGridID( _gridID ))
            {
#if NAVIGATION_LOG
                Debug.Log( "タスク情報の変化がなし、直接完了させる。" );
#endif
                if( finishCallback_ != null    ) finishCallback_.Invoke   ( navigationRoute_ , navigationResult_ , _gridID , true );
                if( temFinishCallback_ != null ) temFinishCallback_.Invoke( navigationRoute_ , navigationResult_ , _gridID , true );
                return false;
            }
            else
            {
                ClearLastInfo();
                gridID_                = _gridID;
                dynamicInfo_.startIdx_ = _startIdx;
                dynamicInfo_.endIdx_   = _endIdx;
                return true;
            }
        }

        /// <summary>
        /// グリッドIDを判断
        /// </summary>
        public bool IsSameGridID( int _gridID )
        {
            return gridID_ == _gridID;
        }

        /// <summary>
        /// スレッド中の情報と同じかどうか判断
        /// </summary>
        public bool IsSameAsThread( NaviGationThreadClass _thread )
        {
            if( _thread == null ) return false;
            if( !NavigationDynamicInfo.IsSame( _thread.Struct_.GetDynamicInfo() , dynamicInfo_ )) return false;            
            return true;
        }

        /// <summary>
        /// 前回の情報と同じかどうか判断
        /// </summary>
        public bool IsSameAsLast( NavigationDynamicInfo _dynamicInfo )
        {
            return NavigationDynamicInfo.IsSame( dynamicInfo_ , _dynamicInfo )
                   && navigationRoute_.Count > 0 
                   && navigationResult_ != NAVIGATION_RESULT.NONE;                     
        }
        
        /// <summary>
        /// 前回情報をクリア
        /// </summary>
        public void ClearLastInfo()
        {
            navigationRoute_.Clear();
            navigationResult_ = NAVIGATION_RESULT.NONE;
        }

        /// <summary>
        /// 結果処理
        /// </summary>
        public void NavigationFinish( NativeArray<int> _resultInfo , NativeArray<Vector2Int> _navigationRoute )
        {
            //ルートの長さは1以上の場合、代入
            if( _resultInfo[ 1 ] >= 1 )
            {
                int routeLength = _resultInfo[ 1 ];
                for( int nCnt = routeLength - 1 ; nCnt >= 0 ; nCnt-- )
                {
                    navigationRoute_.Add( _navigationRoute[ nCnt ]);
                }
            }
            else
            {
                Debug.LogError( "経路の長さは0" );
            }
            navigationResult_ = ( NAVIGATION_RESULT )_resultInfo[ 0 ];
            if( temFinishCallback_ != null ) temFinishCallback_.Invoke( navigationRoute_ , navigationResult_ , gridID_ , false );
            if( finishCallback_    != null ) finishCallback_   .Invoke( navigationRoute_ , navigationResult_ , gridID_ , false );
        }
    }

    /// <summary>
    /// スレッドクラス
    /// </summary>
    private class NaviGationThreadClass
    {
        private AStarNavigationThread struct_;
        public ref AStarNavigationThread Struct_{ get{ return ref struct_;}}

        /// <summary>
        /// コンストラクタ
        /// </summary> 
        public NaviGationThreadClass(int _rowNum, int _colNum , NativeArray<UnitInfo> _gridInfo )
        {
            struct_ = AStarNavigationThread.CreateNew(_rowNum , _colNum , _gridInfo );
        }
    }        

    /// <summary>
    /// ナビゲーションスレッド(A*)
    /// </summary>
#if USING_BURST    
    [BurstCompile]
#endif
    private struct AStarNavigationThread : IJob
    {
        //....变量....//

        private JobHandle handle_;  //Handle
        private bool isExecuting_;  //実行中

        private int   rowNum_;          //ユニット行数
        private int   colNum_;          //ユニット列数
        private int   allNum_;          //ユニット総数
        private int   unitLengh_;       //ユニット長さ
        private int   unitDiagonal_;    //ユニット対角線の長さ

        [ReadOnly ] private NativeArray<UnitInfo> gridInfo_;   //グリッド情報
        [WriteOnly] private NativeArray<int>      resultInfo_; //結果情報(0: 結果 1: 経路のノート数)
        [ReadOnly ] private NativeArray<int>      blockType_;  //障害物タイプ

        private NativeArray<bool>       isDirPass_;     //通行可能の方向を判断するためのフラグ（上下左右のみ計算する） 

        private NativeArray<int>        unitG_;         //ユニットG値       
        private NativeArray<int>        unitH_;         //ユニットH値
        private AStarBinaryHeap         unitF_;         //ユニットF値                                                
        private NativeArray<Vector2Int> unitParent_;    //親ユニット

        private NativeArray<Vector2Int> openArray_;          //Openリスト
        private NativeArray<int>        openArrayIdx_;       //Openリスト用インデックス
        private int                     openArrayUsingNum_;  //Openリストの使用数

        //private NativeArray<Vector2Int> closeArray_;         //Closeリスト
        private NativeArray<int>        closeArrayIdx_;      //Closeリスト用インデックス
        private int                     closeArrayUsingNum_; //Closeリストの使用数

        private NativeArray<Vector2Int> resultRoute_;    //計算した経路
        private NativeArray<Vector2Int> optResultRoute_; //最適化した経路

        private NavigationDynamicInfo dynamicInfo_;   //ダイナミック情報
        
        /// <summary>
        /// 新しいスレッドを作成
        /// </summary>
        public static AStarNavigationThread CreateNew( int _rowNum , int _colNum , NativeArray<UnitInfo> _gridInfo )
        {
            AStarNavigationThread thread = new AStarNavigationThread();

            //初期化
            thread.isExecuting_  = false;
            thread.rowNum_       = _rowNum;
            thread.colNum_       = _colNum;
            thread.allNum_       = _rowNum * _colNum;
            thread.unitLengh_    = 10;
            thread.unitDiagonal_ = 14;

            thread.isDirPass_  = new NativeArray<bool>      ( 4              , Allocator.Persistent );
            thread.unitParent_ = new NativeArray<Vector2Int>( thread.allNum_ , Allocator.Persistent );
            thread.unitG_      = new NativeArray<int>       ( thread.allNum_ , Allocator.Persistent );
            thread.unitH_      = new NativeArray<int>       ( thread.allNum_ , Allocator.Persistent );
            thread.unitF_      = AStarBinaryHeap.GetNew     ( _rowNum        , _colNum              );

            thread.openArrayUsingNum_ = 0; 
            thread.openArray_         = new NativeArray<Vector2Int>( thread.allNum_ , Allocator.Persistent );
            thread.openArrayIdx_      = new NativeArray<int>       ( thread.allNum_ , Allocator.Persistent );

            thread.closeArrayUsingNum_ = 0;
            //thread.closeArray_         = new NativeArray<Vector2Int>( thread.allNum_ , Allocator.Persistent );
            thread.closeArrayIdx_      = new NativeArray<int>       ( thread.allNum_ , Allocator.Persistent );

            thread.resultRoute_    = new NativeArray<Vector2Int>( thread.rowNum_ + thread.colNum_ , Allocator.Persistent );
            thread.optResultRoute_ = new NativeArray<Vector2Int>( thread.rowNum_ + thread.colNum_ , Allocator.Persistent );

            thread.resultInfo_ = new NativeArray<int>( 2 , Allocator.Persistent );
            thread.gridInfo_ = _gridInfo;

            return thread;
        }

        /// <summary>
        /// メモリ解放
        /// </summary>
        private void FreeMemory()
        {
            resultInfo_     .SafeDispose();
            resultRoute_    .SafeDispose();
            optResultRoute_ .SafeDispose();
            closeArrayIdx_  .SafeDispose();
            openArrayIdx_   .SafeDispose();
            //closeArray_   .SafeDispose();
            openArray_      .SafeDispose();
            unitH_          .SafeDispose();
            unitG_          .SafeDispose();
            unitParent_     .SafeDispose();
            isDirPass_      .SafeDispose();
            unitF_          .FreeMemory();
        }

        /// <summary>
        /// 終了
        /// </summary>
        public void Uninit()
        {
            //強制終了
            ForceCompleted();
            //メモリ解放
            FreeMemory();
        }

        /// <summary>
        /// ダイナミック情報を取得
        /// </summary>
        public NavigationDynamicInfo GetDynamicInfo(){ return dynamicInfo_;}

        /// <summary>
        /// 二次元インデックスから一次元インデックスを取得
        /// </summary>
        private int GetIdxFrom2DIdx( Vector2Int _xyIdx )
        {
            return _xyIdx.y * colNum_ + _xyIdx.x;
        }

        /// <summary>
        /// 二次元インデックスから一次元インデックスを取得
        /// </summary>
        private int GetIdxFrom2DIdx( int _xIdx , int _yIdx )
        {
            return _yIdx * colNum_ + _xIdx;
        }

        /// <summary>
        /// 計算用パラメータをリセット
        /// </summary>
        private void ResetCalParam( int[] _resetArray )
        {
            openArrayIdx_ .CopyFrom( _resetArray );
            closeArrayIdx_.CopyFrom( _resetArray );
            openArrayUsingNum_  = 0;
            closeArrayUsingNum_ = 0;
            unitF_.Clear();
        }

        /// <summary>
        /// スレッドスケジュールに代入
        /// </summary>
        public void InSchedue( NavigationTask _task , int[] _resetArray )
        {
            //障害物タイプを設定
            blockType_ = _task.GetBlockType();
            
            //ダイナミック情報を設定
            dynamicInfo_ = _task.GetDynamicInfo();
            
            //始点と終点同じか、始点と終点の間に障害物ない場合、計算がいらない
            if( dynamicInfo_.startIdx_ == dynamicInfo_.endIdx_ || IsPassOnRoute( dynamicInfo_.startIdx_ , dynamicInfo_.endIdx_ , dynamicInfo_.size_ ))
            {
                resultRoute_[0] = dynamicInfo_.endIdx_;
                resultRoute_[1] = dynamicInfo_.startIdx_;
                
                //経路最適化
                int optRouteLength = 0;         
                OptimizationRoute( resultRoute_[0] , resultRoute_[1] , ref optRouteLength );                                                
                
                resultInfo_[0] = (int)NAVIGATION_RESULT.SUCCESS;
                resultInfo_[1] = optRouteLength + 1; //経路ノート数
                isExecuting_   = true;

#if NAVIGATION_LOG
                Debug.Log( "直接移動可能" );
#endif
            }
            //計算必要
            else
            {
                if( !isExecuting_ )
                {
                    ResetCalParam( _resetArray );
                    isExecuting_ = true;
                    handle_      = this.Schedule();
                }
                else
                {
                    Debug.LogError( "スレッド実行中..." );
                }
            }
        }

        /// <summary>
        /// 完成したかどうか判断
        /// </summary>
        public bool IsCompleted()
        {
            if( handle_.IsCompleted && isExecuting_ )
            {
                handle_.Complete();
                isExecuting_ = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 強制終了
        /// </summary>
        public void ForceCompleted()
        {
            handle_.Complete();
        }

        /// <summary>
        /// 経路を取得
        /// </summary>
        public NativeArray<Vector2Int> GetRoute()
        {
            if( !isExecuting_ )
            {
                return optResultRoute_;
            }
            else
            {
                Debug.LogError( "スレッド実行中..." );
                return default;
            }            
        }

        /// <summary>
        /// 結果情報を取得
        /// </summary>
        public NativeArray<int> GetResultInfo()
        {
            if( !isExecuting_ )
            {
                return resultInfo_;
            }
            else
            {
                Debug.LogError( "スレッド実行中..." );
                return default;
            }
        }

        /// <summary>
        /// スレッド実行
        /// </summary>
        public void Execute()
        {
            AStarNavigationCalculate();
        }

        /// <summary>
        /// ナビゲーション処理(A*)         　　 
        /// </summary>        
        private void AStarNavigationCalculate()
        {
            //始点をOpenリストに追加
            openArray_[ openArrayUsingNum_ ] = dynamicInfo_.startIdx_;
            openArrayIdx_[ GetIdxFrom2DIdx( dynamicInfo_.startIdx_ )] = openArrayUsingNum_;
            openArrayUsingNum_++;

            //始点の親ユニットインデックスを設定
            unitParent_[ GetIdxFrom2DIdx( dynamicInfo_.startIdx_ )] = new Vector2Int( -1 , -1 );

            //始点H、G、F値を設定            
            int startH = ( Mathf.Abs( dynamicInfo_.endIdx_.x - dynamicInfo_.startIdx_.x ) + Mathf.Abs( dynamicInfo_.endIdx_.y - dynamicInfo_.startIdx_.y )) * unitLengh_;
            unitH_[ GetIdxFrom2DIdx( dynamicInfo_.startIdx_ )] = startH;
            unitG_[ GetIdxFrom2DIdx( dynamicInfo_.startIdx_ )] = 0;
            unitF_.Insert( unitH_[ GetIdxFrom2DIdx( dynamicInfo_.startIdx_ )] + unitG_[ GetIdxFrom2DIdx( dynamicInfo_.startIdx_ )] , dynamicInfo_.startIdx_ );

            //最小H値のユニットを取得
            Vector2Int minimunHUnit = dynamicInfo_.startIdx_;

            //最後のユニットインデックス
            Vector2Int lastUnitIdx = new Vector2Int( -1 , -1 );

            //デッドエンド判断
            bool isDeadEnd = true;

            //..........経路計算処理..........
            {
                int errorCount = 0;
                //親インデックスを設定           
                Vector2Int parent = dynamicInfo_.startIdx_;
                while( openArrayUsingNum_ != 0 )
                {
                    //最小F値のユニットを親ユニットにとして設定し、Closeリストに移動させ
                    AStarBinaryHeap.HeapNode minNode = unitF_.TakeOutTopNode();
                    parent = minNode.unitIdx_;

                    //Openリストから削除
                    int parentIdx = openArrayIdx_[ GetIdxFrom2DIdx( parent )];              //インデックスを保存               
                    openArrayIdx_[ GetIdxFrom2DIdx( parent )] = -1;                         //インデックスをOpenリストから削除
                    openArray_[ parentIdx ] = openArray_[ openArrayUsingNum_ - 1 ];         //Openリストの最後要素を代入
                    openArrayIdx_[ GetIdxFrom2DIdx( openArray_[ parentIdx ])] = parentIdx;  //インデックスを更新
                    openArrayUsingNum_--;

                    //Closeリストに代入
                    //closeArray_[ closeArrayUsingNum_ ] = parent;                        //代入               
                    closeArrayIdx_[ GetIdxFrom2DIdx( parent )] = closeArrayUsingNum_;   //インデックスを更新
                    closeArrayUsingNum_++;

                    //親インデックスを最後のインデックスに設定                    
                    lastUnitIdx = parent;

                    //***************************
                    //方向参照：
                    //左     ..0
                    //右     ..1 
                    //下     ..2 
                    //上     ..3
                    //左上   ..4
                    //右上　　..5
                    //右下　　..6
                    //左下　　..7
                    //***************************

                    //ユニットチェック              
                    Vector2Int nearSideUnitIdx = Vector2Int.zero;       //近いユニットインデックス
                    Vector2Int outSideUnitIdx = Vector2Int.zero;        //遠いユニットインデックス
                    int connectDirA = 0;                                //隣と連結する方向A(斜め専用)
                    int connectDirB = 0;                                //隣と連結する方向B(斜め専用)
                    int outLength = -1;                                 //遠いユニットインデックス数（正面専用）

                    int checkWayNum = 8;
                    for( int nIdx = 0 ; nIdx < checkWayNum ; nIdx++ )
                    {
                        //数値を設定
                        switch( nIdx )
                        {
                            //左
                            case 0:
                                nearSideUnitIdx.x = parent.x - 1;
                                nearSideUnitIdx.y = parent.y;
                                outSideUnitIdx.x  = parent.x - 1;
                                outSideUnitIdx.y  = parent.y;
                                outLength         = dynamicInfo_.size_.y;
                                break;
                            //右
                            case 1:
                                nearSideUnitIdx.x = parent.x + 1;
                                nearSideUnitIdx.y = parent.y;
                                outSideUnitIdx.x  = parent.x + dynamicInfo_.size_.x;
                                outSideUnitIdx.y  = parent.y;
                                outLength         = dynamicInfo_.size_.y;
                                break;
                            //下
                            case 2:
                                nearSideUnitIdx.x = parent.x;
                                nearSideUnitIdx.y = parent.y - 1;
                                outSideUnitIdx.x  = parent.x;
                                outSideUnitIdx.y  = parent.y - 1;
                                outLength         = dynamicInfo_.size_.x;
                                break;
                            //上
                            case 3:
                                nearSideUnitIdx.x = parent.x;
                                nearSideUnitIdx.y = parent.y + 1;
                                outSideUnitIdx.x  = parent.x;
                                outSideUnitIdx.y  = parent.y + dynamicInfo_.size_.y;
                                outLength         = dynamicInfo_.size_.x;
                                break;
                            //左上
                            case 4:
                                nearSideUnitIdx.x = parent.x - 1;
                                nearSideUnitIdx.y = parent.y + 1;
                                outSideUnitIdx.x  = parent.x - 1;
                                outSideUnitIdx.y  = parent.y + dynamicInfo_.size_.y;
                                connectDirA       = 0;
                                connectDirB       = 3;
                                break;
                            //右上
                            case 5:
                                nearSideUnitIdx.x = parent.x + 1;
                                nearSideUnitIdx.y = parent.y + 1;
                                outSideUnitIdx.x  = parent.x + dynamicInfo_.size_.x;
                                outSideUnitIdx.y  = parent.y + dynamicInfo_.size_.y;
                                connectDirA       = 1;
                                connectDirB       = 3;
                                break;
                            //右下
                            case 6:
                                nearSideUnitIdx.x = parent.x + 1;
                                nearSideUnitIdx.y = parent.y - 1;
                                outSideUnitIdx.x  = parent.x + dynamicInfo_.size_.x;
                                outSideUnitIdx.y  = parent.y - 1;
                                connectDirA       = 1;
                                connectDirB       = 2;
                                break;
                            //左下
                            case 7:
                                nearSideUnitIdx.x = parent.x - 1;
                                nearSideUnitIdx.y = parent.y - 1;
                                outSideUnitIdx.x  = parent.x - 1;
                                outSideUnitIdx.y  = parent.y - 1;
                                connectDirA       = 0;
                                connectDirB       = 2;
                                break;
                            default:
                                break;
                        }

                        //正面の場合
                        if( nIdx <= 3 )
                        {
                            //.......
                            //　遠いユニットインデックスを判断する処理：
                            //　・インデックス有効判断                            
                            //　・ユニット通行可能判断
                            //
                            //　近いユニットインデックスを判断する処理：
                            //　・インデックス有効判断
                            //　・インデックスはCloseリストに入ったかどうか判断
                            //.......

                            //フラグリセット
                            isDirPass_[ nIdx ] = true;

                            //遠いユニットインデックスを判断する処理
                            for( int nOut = 0 ; nOut < outLength ; nOut++ )
                            {
                                Vector2Int outIdx = nIdx <= 1 ? new Vector2Int( outSideUnitIdx.x , outSideUnitIdx.y + nOut ) :
                                                                new Vector2Int( outSideUnitIdx.x + nOut , outSideUnitIdx.y );

                                if( !IsIdxVaildCheck( outIdx ) ||    //遠いユニットインデックス有効判断
                                    IsBlock( outIdx ))               //ユニット通行可能判断
                                {
                                    isDirPass_[ nIdx ] = false;
                                    break;
                                }
                            }

                            if( isDirPass_[ nIdx ])
                            {
                                //遠いユニットインデックスと近いユニットインデックスが違う場合
                                if( outLength > 1 )
                                {
                                    if( !IsIdxVaildCheck( nearSideUnitIdx ))   //近いユニットインデックス有効判断
                                    {
                                        isDirPass_[ nIdx ] = false;
                                    }
                                }
                            }

                            if( isDirPass_[ nIdx ])
                            {
                                //近いユニットインデックスを判断する処理
                                if( closeArrayIdx_[ GetIdxFrom2DIdx( nearSideUnitIdx )] != -1 )   //近いユニットインデックスはCloseリストに入ったかどうか判断
                                {
                                    isDirPass_[ nIdx ] = false;
                                }
                            }

                            if( !isDirPass_[ nIdx ]) 
                            { 
                                continue;
                            }
                        }
                        //斜めの場合
                        else
                        {
                            //.......
                            //　遠いユニットインデックスを判断する処理：
                            //　・インデックス有効判断
                            //　・ユニット通行可能判断
                            //　・隣と連結する方向Aの通行可能判断
                            //　・隣と連結する方向Bの通行可能判断
                            //
                            //　近いユニットインデックスを判断する処理：
                            //　・インデックス有効判断
                            //　・インデックスはCloseリストに入ったかどうか判断
                            //.......

                            //遠いユニットインデックスを判断する処理
                            if( !IsIdxVaildCheck( outSideUnitIdx )  ||  //インデックス有効判断
                                IsBlock( outSideUnitIdx )           ||  //ユニット通行可能判断
                                !isDirPass_[ connectDirA ]          ||  //隣と連結する方向Aの通行可能判断
                                !isDirPass_[ connectDirB ])             //隣と連結する方向Bの通行可能判断
                            {
                                continue;
                            }

                            //遠いユニットインデックスと近いユニットインデックスが違う場合
                            if( outLength > 1 )
                            {
                                if( !IsIdxVaildCheck( nearSideUnitIdx ))  //近いユニットインデックス有効判断
                                {
                                    continue;
                                }
                            }

                            //近いユニットインデックスを判断する処理                        
                            if( closeArrayIdx_[ GetIdxFrom2DIdx( nearSideUnitIdx )] != -1)   //近いユニットインデックスはCloseリストに入ったかどうか判断
                            {
                                continue;
                            }
                        }

                        //近いユニットインデックスはOpenリストに入ってない場合
                        if( openArrayIdx_[ GetIdxFrom2DIdx( nearSideUnitIdx )] == -1 )
                        {
                            //Openリストに代入
                            openArray_[ openArrayUsingNum_ ] = nearSideUnitIdx;                         //代入
                            openArrayIdx_[ GetIdxFrom2DIdx( nearSideUnitIdx )] = openArrayUsingNum_;    //インデックス更新
                            openArrayUsingNum_++;

                            unitParent_[ GetIdxFrom2DIdx( nearSideUnitIdx )] = parent;    //親インデックスに設定

                            //終点に到着！！
                            if( nearSideUnitIdx == dynamicInfo_.endIdx_ ){ break;}

                            //FGH値を計算( F = G + H )
                            {
                                //ウェイトを計算
                                int weight = ( nIdx <= 3 ? unitLengh_ : unitDiagonal_ ) * ( 1 + GetUnitWeight( nearSideUnitIdx ));

                                //G値を計算
                                unitG_[ GetIdxFrom2DIdx( nearSideUnitIdx )] = unitG_[ GetIdxFrom2DIdx( parent )] + weight;                                
                                
                                //直線でH値を計算
                                int h = ( int )(( dynamicInfo_.endIdx_ - nearSideUnitIdx ).magnitude * unitLengh_ );

                                //Manhattan distanceでH値を計算                                
                                //int h = ( Mathf.Abs( dynamicInfo_.endIdx_.x - nearSideUnitIdx.x ) + Mathf.Abs( dynamicInfo_.endIdx_.y - nearSideUnitIdx.y )) * unitLengh_;

                                unitH_[ GetIdxFrom2DIdx( nearSideUnitIdx )] = h;

                                //最小H値のユニットを更新(デッドエンドの場合利用)
                                if( unitH_[ GetIdxFrom2DIdx( minimunHUnit )] > h )
                                {
                                    minimunHUnit = nearSideUnitIdx;
                                }
                                unitF_.Insert( unitG_[ GetIdxFrom2DIdx( nearSideUnitIdx )] + unitH_[ GetIdxFrom2DIdx( nearSideUnitIdx )], nearSideUnitIdx );
                            }
                        }
                        //近いユニットインデックスはOpenリストに入った場合
                        else
                        {
                            //ウェイトを計算
                            int weight = ( nIdx <= 3 ? unitLengh_ : unitDiagonal_ ) * ( 1 + GetUnitWeight( nearSideUnitIdx ));

                            //新しいG値を計算
                            int newG = unitG_[ GetIdxFrom2DIdx( parent )] + weight;
                            
                            //元のG値は新しいG値より大きい場合
                            if( unitG_[ GetIdxFrom2DIdx( nearSideUnitIdx )] > newG )
                            {
                                //親インデックス更新                                
                                unitParent_[ GetIdxFrom2DIdx( nearSideUnitIdx )] = parent;

                                //GとF値を更新(H値固定)                               
                                unitF_.Remove( nearSideUnitIdx );
                                unitG_[ GetIdxFrom2DIdx( nearSideUnitIdx )] = newG;
                                unitF_.Insert( unitG_[ GetIdxFrom2DIdx( nearSideUnitIdx )] + unitH_[ GetIdxFrom2DIdx( nearSideUnitIdx )] , nearSideUnitIdx );
                            }
                        }
                    }

                    //最後のインデックスはOpenリストに入った場合、計算終了
                    if( openArrayIdx_[ GetIdxFrom2DIdx( dynamicInfo_.endIdx_ )] != -1 )
                    {
                        lastUnitIdx   = dynamicInfo_.endIdx_;
                        isDeadEnd = false;
                        break;
                    }

                    errorCount++;
                    if( errorCount > allNum_ )
                    {
                        Debug.LogError( "Loop over..." );
                        break;
                    }
                }
            }

            //..........計算した経路を整理..........            
            int routeLength = -1; //経路の長さ

            if( isDeadEnd ) lastUnitIdx = minimunHUnit;  //デッドエンドの場合、最小H値を持ってるユニットインデックスを終点に設定

            if( IsIdxVaildCheck( lastUnitIdx ))
            {
                //終点インデックスをこのまま代入
                routeLength++;
                resultRoute_[ routeLength ] = lastUnitIdx;
                
                Vector2Int nowUnitIdx = unitParent_[ GetIdxFrom2DIdx( lastUnitIdx )]; //現在インデックス
                Vector2Int saveDir = nowUnitIdx - lastUnitIdx;                        //前のインデックスから現在のインデックスまでの方向
                int errorCount = 0;

                while( resultRoute_[ routeLength ] != dynamicInfo_.startIdx_ )
                {
                    //現在のインデックスは始点インデックスの場合、このまま代入                   
                    if( nowUnitIdx == dynamicInfo_.startIdx_ )
                    {
                        //経路中に二つ以上のインデックスが存在する場合                        
                        if( routeLength >= 1 )
                        {
                            int prevUnitIdx = routeLength - 1;
                            Vector2Int checkUnitIdx = resultRoute_[ prevUnitIdx ];
                            //現在のインデックスからチェックインデックスの間に障害物あるかどうか判断                            
                            if( IsPassOnRoute( nowUnitIdx , checkUnitIdx , dynamicInfo_.size_ ))
                            {
                                //障害物がない場合、インデックスを1個分を戻す
                                routeLength--;
                            }
                        }

                        routeLength++;
                        resultRoute_[ routeLength ] = nowUnitIdx;
                    }
                    else
                    //現在のインデックスは始点インデックスではない場合、代入するかどうか方向で判断する
                    {
                        Vector2Int nextUnitIdx = unitParent_[ GetIdxFrom2DIdx( nowUnitIdx )];
                        Vector2Int nowDir = nextUnitIdx - nowUnitIdx;
                        if( nowDir != saveDir )
                        {
                            //経路中に二つ以上のインデックスが存在する場合   
                            if( routeLength >= 1 )
                            {
                                int prevUnitIdx = routeLength - 1;
                                Vector2Int checkUnitIdx = resultRoute_[ prevUnitIdx ];
                                //現在のインデックスからチェックインデックスの間に障害物あるかどうか判断
                                if( IsPassOnRoute( nowUnitIdx , checkUnitIdx , dynamicInfo_.size_ ))
                                {
                                    //障害物がない場合、インデックスを1個分を戻す
                                    routeLength--;
                                }
                            }
                            routeLength++;
                            resultRoute_[ routeLength ] = nowUnitIdx;                        
                            saveDir = nowDir;
                        }
                        nowUnitIdx = nextUnitIdx;
                    }

                    errorCount++;
                    if( errorCount > allNum_ )
                    {
                        Debug.LogError( "Loop over..." );
                        break;
                    }
                }

                //始点と終了同じ場合、始点をコピーして、追加する
                if( routeLength == 0 )
                {
                    routeLength++;
                    resultRoute_[ routeLength ] = resultRoute_[ routeLength - 1 ];
                }
                
                //経路を最適化
                int optRouteLength = 0;
                for( int nCnt = 0 ; nCnt < routeLength ; nCnt ++ )
                {
                    OptimizationRoute( resultRoute_[ nCnt ] , resultRoute_[ nCnt + 1 ] , ref optRouteLength );                                                
                }

                //結果を設定
                resultInfo_[ 0 ] = isDeadEnd ? (int)NAVIGATION_RESULT.DEADEND : (int)NAVIGATION_RESULT.SUCCESS;               
                //経路インデックス数を設定
                resultInfo_[ 1 ] = optRouteLength + 1;
            }
            else
            {
                Debug.LogError( "終点インデックス無効" );
            }
        }

        /// <summary>
        /// 二つユニットの間に障害物あるかどうか判断
        /// </summary>
        private bool IsPassOnRoute( Vector2Int _startIdx, Vector2Int _endIdx , Vector2Int _size )
        {
            int   startX    = _startIdx.x;        
            int   startY    = _startIdx.y;
            int   endX      = _endIdx.x;
            int   endY      = _endIdx.y;
            int   nowX      = startX;
            int   nowY      = startY;  
            int   disX      = Mathf.Abs( endX - startX );
            int   disY      = Mathf.Abs( endY - startY );     
            int   sizeX     = Mathf.Abs( _size.x );
            int   sizeY     = Mathf.Abs( _size.y );
            int   unitX     = 0;
            int   unitY     = 0;            
            int   lineCount = 0;            
            
            if( endX - startX > 0 ) unitX = 1;
            if( endX - startX < 0 ) unitX = -1;
            if( endY - startY > 0 ) unitY = 1;
            if( endY - startY < 0 ) unitY = -1;

            if( disX > disY )
            {
                float split = disY == 0 ? 0 : disX / (float)disY;
                
                nowY -= unitY;
                while( nowX != endX )
                {
                    bool isSplitInteger = true;
                    if( nowY != endY )
                    {
                        float temValue = split * lineCount;   
                        if( nowX == startX + (int)temValue * unitX )
                        {
                            nowY += unitY; 
                            lineCount ++;

                            int calY = unitY < 0 ? nowY + unitY : nowY + sizeY;
                            for( int nX = 0 ; nX < sizeX ; nX ++ )
                            {
                                int calX = nowX + nX;
                                if( IsIdxVaildCheck( calX , calY ))
                                {
                                    if(    IsBlock( calX , calY ) 
                                        || GetUnitWeight( calX , calY ) > 0 )
                                    {
                                        return false;
                                    }
                                }
                                else
                                {
                                    return false;
                                }                                
                            }

                            float intCheck = split * lineCount - split;
                            if( !Mathf.Approximately( intCheck - (int)intCheck , 0 ))
                            {
                                isSplitInteger = false;
                            }                                              
                        }
                    }

                    nowX += unitX;

                    int cntBeginY  = unitY > 0 && !isSplitInteger ? -1 : 0;
                    int cntFinishY = unitY < 0 && !isSplitInteger ? sizeY + 1 : sizeY;
                    for( int nY = cntBeginY ; nY < cntFinishY ; nY ++ )
                    {
                        int calX = unitX > 0 ? nowX + ( sizeX - 1 ) : nowX;
                        int calY = nowY + nY;                    
                        if( IsIdxVaildCheck( calX , calY ))
                        {
                            if(    IsBlock( calX , calY ) 
                                || GetUnitWeight( calX , calY ) > 0 )
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }                        
                    }

                    if( nowY != endY )               
                    {
                        int calX = unitX > 0 ? nowX + ( sizeX - 1 ) : nowX;
                        int calY = unitY < 0 ? nowY + unitY : nowY + sizeY;                        
                        if( IsIdxVaildCheck( calX , calY ))
                        {
                            if(    IsBlock( calX , calY ) 
                                || GetUnitWeight( calX , calY ) > 0 )
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }                                             
                    }
                }
            }
            else
            {
                float split = disX == 0 ? 0 : disY / (float)disX;
                
                nowX -= unitX;
                while( nowY != endY )
                {
                    bool isSplitInteger = true;
                    if( nowX != endX )
                    {
                        float temValue = split * lineCount;   
                        if( nowY == startY + (int)temValue * unitY )
                        {
                            nowX += unitX; 
                            lineCount ++;

                            int calX = unitX < 0 ? nowX + unitX : nowX + sizeX;
                            for( int nY = 0 ; nY < sizeY ; nY ++ )
                            {
                                int calY = nowY + nY;
                                if( IsIdxVaildCheck( calX , calY ))
                                {
                                    if(    IsBlock( calX , calY ) 
                                        || GetUnitWeight( calX , calY ) > 0 )
                                    {
                                        return false;
                                    }
                                }
                                else
                                {
                                    return false;
                                }                                
                            }

                            float intCheck = split * lineCount - split;
                            if( !Mathf.Approximately( intCheck - (int)intCheck , 0 ))
                            {
                                isSplitInteger = false;
                            }                                              
                        }
                    }

                    nowY += unitY;

                    int cntBeginX  = unitX > 0 && !isSplitInteger ? -1 : 0;
                    int cntFinishX = unitX < 0 && !isSplitInteger ? sizeX + 1 : sizeX;
                    for( int nX = cntBeginX ; nX < cntFinishX ; nX ++ )
                    {
                        int calY = unitY > 0 ? nowY + ( sizeY - 1 ) : nowY;
                        int calX = nowX + nX;                    
                        if( IsIdxVaildCheck( calX , calY ))
                        {
                            if(    IsBlock( calX , calY ) 
                                || GetUnitWeight( calX , calY ) > 0 )
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }                        
                    }

                    if( nowX != endX )               
                    {
                        int calY = unitY > 0 ? nowY + ( sizeY - 1 ) : nowY;
                        int calX = unitX < 0 ? nowX + unitX : nowX + sizeX;                        
                        if( IsIdxVaildCheck( calX , calY ))
                        {
                            if(    IsBlock( calX , calY ) 
                                || GetUnitWeight( calX , calY ) > 0 )
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }                                             
                    }
                }
            }
            return true;      
        }

        /// <summary>
        /// 経路を最適化
        /// Bresenham直線方法で
        /// </summary>
        private void OptimizationRoute( Vector2Int _startIdx , Vector2Int _endIdx , ref int _optRouteLength )
        {
            if( dynamicInfo_.routeType_ == NAVIGATION_ROUTE_TYPE.FREE_DIRECTION || _startIdx == _endIdx )
            {
                optResultRoute_[ _optRouteLength ] = _startIdx;
                _optRouteLength ++;
                optResultRoute_[ _optRouteLength ] = _endIdx;
                return;
            }

            int startX  = _startIdx.x;
            int startY  = _startIdx.y;
            int endX    = _endIdx.x;
            int endY    = _endIdx.y;
            int disX    = endX - startX;
            int disY    = endY - startY;
            int unitX   = ( disX > 0 ) ? 1 : -1;
            int unitY   = ( disY > 0 ) ? 1 : -1;
            int nowX    = startX;
            int nowY    = startY;
            int prevX   = nowX;
            int prevY   = nowY;
            int dirX    = 0;
            int dirY    = 0;
            int eps     = 0;

            endX += unitX;
            endY += unitY;
            disX = Mathf.Abs( disX );
            disY = Mathf.Abs( disY );

            if( disX > disY )
            {
                for( nowX = startX; nowX != endX; nowX += unitX )
                {
                    if( dynamicInfo_.routeType_ == NAVIGATION_ROUTE_TYPE.FOUR_DIRECTION )
                    {
                        if( nowX - prevX != dirX || nowY - prevY != dirY || ( dirX != 0 && dirY != 0 ))
                        {
                            dirX = nowX - prevX;
                            dirY = nowY - prevY;

                            if( optResultRoute_[ _optRouteLength ].x != nowX - unitX || optResultRoute_[ _optRouteLength ].y != nowY )
                            {
                                _optRouteLength ++;
                                optResultRoute_[ _optRouteLength ] = new Vector2Int( nowX - unitX , nowY );                            
                            }
                            _optRouteLength ++;
                        }
                    }
                    else
                    {
                        if( nowX - prevX != dirX || nowY - prevY != dirY )
                        {
                            dirX = nowX - prevX;
                            dirY = nowY - prevY;
                            _optRouteLength ++;
                        }
                    }

                    prevX = nowX;
                    prevY = nowY;

                    optResultRoute_[ _optRouteLength ] = new Vector2Int( nowX , nowY );

                    eps += disY;
                    if(( eps << 1 ) >= disX )
                    {
                        nowY += unitY;
                        eps -= disX;
                    }
                }
            }
            else
            {
                for( nowY = startY ; nowY != endY ; nowY += unitY )
                {
                    if( dynamicInfo_.routeType_ == NAVIGATION_ROUTE_TYPE.FOUR_DIRECTION )
                    {
                        if( nowX - prevX != dirX || nowY - prevY != dirY || ( dirX != 0 && dirY != 0 ))
                        {
                            dirX = nowX - prevX;
                            dirY = nowY - prevY;

                            if( optResultRoute_[ _optRouteLength ].x != nowX || optResultRoute_[ _optRouteLength ].y != nowY - unitY )
                            {
                                _optRouteLength ++;
                                optResultRoute_[ _optRouteLength ] = new Vector2Int( nowX , nowY - unitY );                            
                            }
                            _optRouteLength ++; 
                        }    
                    }
                    else
                    {
                        if( nowX - prevX != dirX || nowY - prevY != dirY )
                        {
                            dirX = nowX - prevX;
                            dirY = nowY - prevY;
                            _optRouteLength ++; 
                        } 
                    }
             
                    prevX = nowX;
                    prevY = nowY;

                    optResultRoute_[ _optRouteLength ] = new Vector2Int( nowX , nowY );
                    
                    eps += disX;
                    if (( eps << 1 ) >= disY)
                    {
                        nowX += unitX;
                        eps -= disY;
                    }
                }
            }                 
        }

        /// <summary>
        /// ユニットのウエイトを取得
        /// </summary>
        private int GetUnitWeight( Vector2Int _idx )
        {
            return GetUnitWeight( _idx.x , _idx.y );
        }

        /// <summary>
        /// ユニットのウエイトを取得
        /// </summary>        
        private int GetUnitWeight( int _xIdx , int _yIdx )
        {
            int weight = gridInfo_[ GetIdxFrom2DIdx( _xIdx , _yIdx )].unitWeight_;
            if( weight < 0 ) weight = 0;
            return weight;
        }

        /// <summary>
        /// 障害物を判断
        /// </summary>
        private bool IsBlock( Vector2Int _idx )
        {
            return IsBlock( _idx.x , _idx.y );
        }

        /// <summary>
        /// 障害物を判断
        /// </summary>
        private bool IsBlock( int _xIdx , int _yIdx )
        {
            if( !blockType_.IsCreated ){ return false; }

            for( int nCnt = 0 ; nCnt < blockType_.Length ; nCnt ++ )
            {
                if( blockType_[ nCnt ] == gridInfo_[ GetIdxFrom2DIdx( _xIdx , _yIdx )].unitType_ ) return true;
            }
            return false;
        }

        /// <summary>
        /// インデックス有効を判断
        /// </summary>
        private bool IsIdxVaildCheck( Vector2Int _unitIdx )
        {
            return IsIdxVaildCheck( _unitIdx.x , _unitIdx.y );
        }

        /// <summary>
        /// インデックス有効を判断
        /// </summary>
        private bool IsIdxVaildCheck( int _unitX , int _unitY )
        {
            if (_unitX >= colNum_ ||
                _unitX < 0 ||
                _unitY >= rowNum_ ||
                _unitY < 0)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 実行中？
        /// </summary>
        public bool IsExecuting()
        {
            return isExecuting_;
        }

        /// <summary>
        /// A*専用binaryHeap
        /// </summary>
        private struct AStarBinaryHeap
        {
            /// <summary>
            /// ヒープノート
            /// </summary>
            public struct HeapNode
            {
                public int        value_;   //値
                public Vector2Int unitIdx_; //ユニットインデックス

                /// <summary>
                /// 生成
                /// </summary>
                public static HeapNode GetNew( int _value , Vector2Int _unitIdx )
                {
                    HeapNode resetNode;
                    resetNode.value_   = _value;
                    resetNode.unitIdx_ = _unitIdx;
                    return resetNode;
                }

                /// <summary>
                /// リセット
                /// </summary>
                public static HeapNode Reset()
                {
                    HeapNode resetNode;
                    resetNode.value_   = -1;
                    resetNode.unitIdx_ = new Vector2Int(-1, -1);
                    return resetNode;
                }
            }

            private NativeArray<HeapNode> heapNodeArray_;   //ヒープノート配列
            private int                   heapVaildNum_;    //ヒープノート有効数
            private HeapNode              removedNode_;     //削除したヒープノート
            private NativeArray<int>      heapIdxArray_;    //ヒープインデックス配列
            //private int                   heapRowLength_;   //ヒープの行数
            private int                   heapColLength_;   //ヒープの列数

            /// <summary>
            /// 生成
            /// </summary>
            public static AStarBinaryHeap GetNew( int _heapRowLength , int _heapColLength )
            {
                AStarBinaryHeap heap;
                //初期化
                heap.heapNodeArray_ = new NativeArray<HeapNode>( _heapRowLength * _heapColLength , Allocator.Persistent );
                heap.heapVaildNum_  = 0;
                heap.removedNode_   = HeapNode.Reset();
                heap.heapIdxArray_  = new NativeArray<int>( _heapRowLength * _heapColLength , Allocator.Persistent );
                //heap.heapRowLength_ = _heapRowLength;
                heap.heapColLength_ = _heapColLength;

                int length = heap.heapNodeArray_.Length;
                for( int nCnt = 0 ; nCnt < length ; nCnt++ )
                {
                    heap.heapNodeArray_[nCnt] = HeapNode.Reset();
                }

                return heap;
            }

            /// <summary>
            /// メモリ解放
            /// </summary>
            public void FreeMemory()
            {
                heapNodeArray_.SafeDispose();
                heapIdxArray_.SafeDispose();
            }

            /// <summary>
            /// 2次元インデックスから1次元インデックスを取得
            /// </summary>
            private int GetArrayIdxFrom2DIdx( Vector2Int _xyIdx )
            {
                return _xyIdx.y * heapColLength_ + _xyIdx.x;
            }

            /// <summary>
            /// 左の子ノートのインデックスを取得
            /// </summary>
            private int GetLeftNodeIdx( int _myIdx )
            {
                return 2 * _myIdx + 1;
            }

            /// <summary>
            /// 右の子ノートのインデックスを取得
            /// </summary>
            private int GetRightNodeIdx( int _myIdx )
            {
                return 2 * _myIdx + 2;
            }

            /// <summary>
            /// 親ノートのインデックスを取得
            /// </summary>
            private int GetParentNodeIdx( int _myIdx )
            {
                return ( _myIdx - 1 ) < 0 ? -1 : Mathf.FloorToInt(( _myIdx - 1 ) * 0.5f );
            }

            /// <summary>
            /// インデックス有効を判断
            /// </summary>
            private bool IsIdxVaild( int _idx )
            {
                return _idx >= 0 && _idx < heapVaildNum_;
            }

            /// <summary>
            /// 新しいノートを差し込み
            /// </summary>
            public void Insert( int _value , Vector2Int _unitIdx )
            {
                //オーバーフロー
                if( heapVaildNum_ >= heapNodeArray_.Length )
                {
                    Debug.LogError( "HeapNodeArray is Overflow" );
                    return;
                }

                //新しいノートを設定
                heapNodeArray_[ heapVaildNum_ ] = HeapNode.GetNew( _value , _unitIdx );

                //インデックスを設定
                heapIdxArray_[ GetArrayIdxFrom2DIdx( _unitIdx )] = heapVaildNum_;

                //新しいノートのインデックスを取得
                int nowIdx = heapVaildNum_;

                int errorCount = 0;
                while( true )
                {
                    //親ノートのインデックスを取得
                    int parentNoteIdx = GetParentNodeIdx( nowIdx );

                    //親の値は自身より大きい場合
                    if( IsIdxVaild( parentNoteIdx ) && heapNodeArray_[ nowIdx ].value_.CompareTo( heapNodeArray_[ parentNoteIdx ].value_ ) < 0 )
                    {
                        //親ノートと自身の位置を交換                                 
                        HeapNode parent = heapNodeArray_[ parentNoteIdx ];                                                //親を保存                  
                        heapNodeArray_[ parentNoteIdx ] = heapNodeArray_[ nowIdx ];                                       //自身を親の位置に代入
                        heapIdxArray_[ GetArrayIdxFrom2DIdx( heapNodeArray_[ parentNoteIdx ].unitIdx_ )] = parentNoteIdx; //自身のインデックスを更新
                        heapNodeArray_[ nowIdx ] = parent;                                                                //親を自身の位置に代入
                        heapIdxArray_[ GetArrayIdxFrom2DIdx( heapNodeArray_[ nowIdx ].unitIdx_ )] = nowIdx;               //親のインデックスを更新                        
                        nowIdx = parentNoteIdx;
                    }
                    else
                    {
                        break;
                    }

                    errorCount++;
                    if( errorCount >= heapNodeArray_.Length )
                    {
                        Debug.LogError("Loop over...");
                        break;
                    }
                }

                heapVaildNum_++;
            }

            /// <summary>
            /// 一番上のノートを引き出す
            /// </summary>
            public HeapNode TakeOutTopNode()
            {
                return _Remove( 0 );
            }

            /// <summary>
            /// 削除
            /// </summary>
            public void Remove( Vector2Int _unitIdx )
            {
                _Remove( GetNodeIdx( _unitIdx ));
            }

            /// <summary>
            /// 削除した後、削除したノートをリターン
            /// </summary>
            private HeapNode _Remove( int _removeIdx )
            {
                if( _removeIdx >= heapVaildNum_ )
                {
                    Debug.LogError( "Idx is Overflow" );
                    return HeapNode.Reset();
                }

                //削除したいノートを保存
                removedNode_.value_   = heapNodeArray_[ _removeIdx ].value_;
                removedNode_.unitIdx_ = heapNodeArray_[ _removeIdx ].unitIdx_;

                //ノート配列中に一つ値だけ存在する場合
                if( heapVaildNum_ == 1 )
                {
                    heapNodeArray_[ 0 ] = HeapNode.Reset();
                    heapVaildNum_ = 0;
                    return removedNode_;
                }

                //削除したいノートをノート配列の最後ところに移動
                heapNodeArray_[ _removeIdx ] = heapNodeArray_[ heapVaildNum_ - 1 ];
                heapIdxArray_[ GetArrayIdxFrom2DIdx( heapNodeArray_[ _removeIdx ].unitIdx_ )] = _removeIdx; //インデックスを更新              
                heapNodeArray_[ heapVaildNum_ - 1 ] = HeapNode.Reset();                                     //最後のノート持ってる情報をクリア
                heapVaildNum_--;                                                                            //ノート有効数を減らす       

                int nowIdx = _removeIdx;
                int errorCount = 0;
                while( true )
                {
                    //現在ノートの左右子ノートのインデックスを取得
                    int leftIdx  = GetLeftNodeIdx( nowIdx );
                    int rightIdx = GetRightNodeIdx( nowIdx );

                    //左ノートのみ
                    if( IsIdxVaild( leftIdx ) && !IsIdxVaild( rightIdx ))
                    {
                        if( heapNodeArray_[ nowIdx ].value_ > heapNodeArray_[ leftIdx ].value_ )
                        {
                            //位置を交換         
                            HeapNode tem = heapNodeArray_[ leftIdx ];                                             //左ノート保存                 
                            heapNodeArray_[ leftIdx ] = heapNodeArray_[ nowIdx ];                                 //現在ノートを左ノートのところに代入
                            heapIdxArray_[ GetArrayIdxFrom2DIdx( heapNodeArray_[ leftIdx ].unitIdx_ )] = leftIdx; //現在ノートのインデックスを更新
                            heapNodeArray_[ nowIdx ] = tem;                                                       //左ノートを現在のノートの元位置に移動
                            heapIdxArray_[ GetArrayIdxFrom2DIdx( heapNodeArray_[ nowIdx ].unitIdx_ )] = nowIdx;   //左ノートのインデックスを更新                 
                            nowIdx = leftIdx;
                        }
                        else
                        {
                            break;
                        }
                    }
                    //右ノートのみ
                    else if( !IsIdxVaild( leftIdx ) && IsIdxVaild( rightIdx ))
                    {
                        if( heapNodeArray_[ nowIdx ].value_ > heapNodeArray_[ rightIdx ].value_ )
                        {
                            //交换位置           
                            HeapNode tem = heapNodeArray_[ rightIdx ];                                                //右ノート保存                   
                            heapNodeArray_[ rightIdx ] = heapNodeArray_[ nowIdx ];                                    //現在ノートを右ノートのところに代入
                            heapIdxArray_[ GetArrayIdxFrom2DIdx( heapNodeArray_[ rightIdx ].unitIdx_ )] = rightIdx;   //現在ノートのインデックスを更新
                            heapNodeArray_[ nowIdx ] = tem;                                                           //右ノートを現在のノートの元位置に移動
                            heapIdxArray_[ GetArrayIdxFrom2DIdx( heapNodeArray_[ nowIdx ].unitIdx_ )] = nowIdx;       //右ノートのインデックスを更新               
                            nowIdx = rightIdx;
                        }
                        else
                        {
                            break;
                        }
                    }
                    //両方とも存在
                    else if( IsIdxVaild( leftIdx ) && IsIdxVaild( rightIdx ))
                    {
                        int useNodeIdx = ( heapNodeArray_[ leftIdx ].value_ < heapNodeArray_[ rightIdx ].value_ ) ? leftIdx : rightIdx;

                        if( heapNodeArray_[ nowIdx ].value_.CompareTo( heapNodeArray_[ useNodeIdx ].value_ ) > 0 )
                        {
                            //交换位置              
                            HeapNode tem = heapNodeArray_[ useNodeIdx ];                                                  //選択されたノート保存                     
                            heapNodeArray_[ useNodeIdx ] = heapNodeArray_[ nowIdx ];                                      //現在ノートを選択されたノートのところに代入
                            heapIdxArray_[ GetArrayIdxFrom2DIdx( heapNodeArray_[ useNodeIdx ].unitIdx_ )] = useNodeIdx;   //現在ノートのインデックスを更新
                            heapNodeArray_[ nowIdx ] = tem;                                                               //選択されたノートを現在のノートの元位置に移動
                            heapIdxArray_[ GetArrayIdxFrom2DIdx( heapNodeArray_[ nowIdx ].unitIdx_ )] = nowIdx;           //選択されたノートのインデックスを更新                      
                            nowIdx = useNodeIdx;
                        }
                        else
                        {
                            break;
                        }
                    }
                    //両方ともない
                    else
                    {
                        break;
                    }

                    errorCount++;
                    if( errorCount >= heapNodeArray_.Length )
                    {
                        Debug.LogError( "Loop over..." );
                        break;
                    }
                }
                return removedNode_;
            }

            /// <summary>
            /// クリア
            /// </summary>
            public void Clear()
            {
                heapVaildNum_ = 0;
            }

            /// <summary>
            /// ノートのインデックスを取得
            /// </summary>
            public int GetNodeIdx( Vector2Int _unitIdx )
            {
                return heapIdxArray_[ GetArrayIdxFrom2DIdx( _unitIdx )];
            }
        }
    }
}

public static class NavigationUtility
{
    /// <summary>
    /// メモリ解放
    /// </summary>
    public static void SafeDispose<T>( ref this NativeArray<T> _nativeArray ) where T : struct
    {
        if( _nativeArray.IsCreated ){ _nativeArray.Dispose();}
    }
}
