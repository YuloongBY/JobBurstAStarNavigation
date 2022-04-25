using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// アクター
    /// </summary>
    public class NavigationActor : MonoBehaviour
    {
        //移動速度
        private const float ACTOR_MOVE_SPEED = 300.0f;

        /// <summary>
        /// ナビゲーション情報
        /// </summary>
        private struct NaviInfo
        {
            public Vector2Int   unitIdx_;   //ユニットインデックス
            public NavigationGrid grid_;      //グリッド
            public NaviInfo( NavigationGrid _grid , Vector2Int _unitIdx )
            {
                unitIdx_ = _unitIdx;
                grid_    = _grid;
            }
        }

        //サイズ
        private Vector2Int size_;
        //移動処理
        private Move move_;
        //ナビゲーショングリッド
        private Dictionary<int , NavigationGrid> naviGrid_;
        //ナビゲーション開始情報
        private NaviInfo naviStartInfo_;      
        //ナビゲーション終了情報    
        private NaviInfo naviEndInfo_;
        //障害物リスト
        private int[] blockList_; 
        //経路タイプ
        private NavigationSystem.NAVIGATION_ROUTE_TYPE routeType_;
       
        void Awake(){}

        void Start()
        {
            //ナビゲーションに登録
            NavigationSystem.Register( this , size_ , routeType_ , NavigationFinish , blockList_ );   
        }

        // Update is called once per frame
        void Update()
        {
            //移動処理更新
            move_.MoveUpdate( Time.deltaTime );            
        }

        void OnDestroy()
        {
            //ナビゲーションの登録を解除
            NavigationSystem.Unregister( this );
        }

        /// <summary>
        /// ナビゲーション開始
        /// </summary>
        public void NavigationStart( Vector3 _targetPosition )
        {
            NavigationStart( this.transform.position , _targetPosition );
        }

        /// <summary>
        /// ナビゲーション開始
        /// </summary>
        public void NavigationStart( Vector3 _startPosition , Vector3 _targetPosition )
        {
            //必要な情報を分析、取得            
            if( AnalyzeNaviInfo( _startPosition , _targetPosition ))
            { 
                //開始と終了のところが同じグリッドの場合
                if( naviStartInfo_.grid_.GetID() == naviEndInfo_.grid_.GetID())
                {
                    move_.MoveStop();
                    NavigationSystem.NavigationStart( this , naviStartInfo_.unitIdx_ , naviEndInfo_.unitIdx_ , naviStartInfo_.grid_.GetID());
                }
                else
                {
                    if( naviStartInfo_.grid_.GetExitIdx( naviEndInfo_.grid_ , out Vector2Int _exitIdx ))
                    {       
                        move_.MoveStop();
                        NavigationSystem.NavigationStart( this , naviStartInfo_.unitIdx_ , _exitIdx , naviStartInfo_.grid_.GetID());
                    }
                }   
            }
        }

        /// <summary>
        /// 必要な情報を分析、取得
        /// </summary>
        private bool AnalyzeNaviInfo( Vector3 _startPosition , Vector3 _targetPosition )
        {
            bool isNaviStartValid = false;
            bool isNaviEndValid   = false;

            foreach( var grid in naviGrid_ )
            {
                if( !isNaviStartValid )
                {
                    isNaviStartValid = grid.Value.GetUnitIdxFromPosition( out Vector2Int startIdx , _startPosition );
                    if( isNaviStartValid )
                    {
                        naviStartInfo_ = new NaviInfo( grid.Value , startIdx );
                    }
                }   
                if( !isNaviEndValid )
                {
                    isNaviEndValid = grid.Value.GetUnitIdxFromPosition( out Vector2Int endIdx , _targetPosition );
                    if( isNaviEndValid )
                    {
                        naviEndInfo_ = new NaviInfo( grid.Value , endIdx );
                    }
                }
            }

            return isNaviStartValid && isNaviEndValid;
        }

        /// <summary>
        /// ナビゲーション完成した時のコールバック
        /// </summary>
        private void NavigationFinish( List<Vector2Int> _route , NavigationSystem.NAVIGATION_RESULT _result , int _gridID , bool _isSameLast )
        {
            if( _result == NavigationSystem.NAVIGATION_RESULT.SUCCESS ||
                _result == NavigationSystem.NAVIGATION_RESULT.DEADEND )
            {
                //グリッドIDは開始ところのグリッドIDが同じの場合
                if( _gridID == naviStartInfo_.grid_.GetID())
                {
                    //アクターの位置で経路の始点無視するかどうかの判断
                    Vector3 start = naviGrid_[ _gridID ].GetUnitCenterPosition( _route[ 0 ]);       
                    Vector3 check = naviGrid_[ _gridID ].GetUnitCenterPosition( _route[ 1 ]);                       
                    Vector3 dis1       = this.transform.position - check;
                    Vector3 dis2       = start - check;     
                        
                    bool isIngoreStart = Mathf.Abs( dis1.x ) < Mathf.Abs( dis2.x ) || Mathf.Abs( dis1.y ) < Mathf.Abs( dis2.y );
                    move_.AddToMoveRoute( this.transform.position );                            
                    for ( int nCnt = isIngoreStart ? 1 : 0 ; nCnt < _route.Count ; nCnt ++ )
                    {
                        move_.AddToMoveRoute( naviGrid_[ _gridID ].GetUnitCenterPosition( _route[ nCnt ]));                
                    }                    
                }
                else
                {
                    //経路を直接代入
                    for ( int nCnt = 0 ; nCnt < _route.Count ; nCnt ++ )
                    {
                        move_.AddToMoveRoute( naviGrid_[_gridID].GetUnitCenterPosition( _route[ nCnt ]));                
                    }
                }
            }
            
            //続けてナビゲーションするかどうか判断
            bool isNavFinish = false;
            if( _result == NavigationSystem.NAVIGATION_RESULT.SUCCESS )
            {
                //グリッドIDは終了ところのグリッドIDが同じの場合
                if( _gridID == naviEndInfo_.grid_.GetID())
                {
                    isNavFinish = true;
                }               
            }
            else
            {
                isNavFinish = true;

                if( _gridID != naviEndInfo_.grid_.GetID())
                {
                    bool isValid = naviGrid_.TryGetValue( _gridID , out NavigationGrid nowGrid );
                    if( isValid )
                    {
                        //次のグリッドを取得
                        var nextGrid = nowGrid.GetNextGrid( naviEndInfo_.grid_ );
                        if( nextGrid )
                        {
                            if( nowGrid.GetExitIdx( nextGrid , out Vector2Int exitIdx ))
                            {
                                var lastIdx = _route[ _route.Count  - 1 ];
                                
                                if( lastIdx.x <= exitIdx.x &&
                                    lastIdx.y <= exitIdx.y &&
                                    lastIdx.x + (size_.x - 1 ) >= exitIdx.x &&
                                    lastIdx.y + (size_.y - 1 ) >= exitIdx.y )
                                {
                                    isNavFinish = false;
                                }
                            }
                        }
                    }
                }
            }

            if( !isNavFinish )
            {
                //グリッドを取得
                bool isValid = naviGrid_.TryGetValue( _gridID , out NavigationGrid nowGrid );
                if( isValid )
                {
                    //次のグリッドを取得
                    var nextGrid = nowGrid.GetNextGrid( naviEndInfo_.grid_ );
                    if( nextGrid != null )
                    {
                        //次のグリッドは最後のグリッドになった場合                        
                        if( nextGrid == naviEndInfo_.grid_ )
                        {
                            if( nextGrid.GetExitIdx( nowGrid , out Vector2Int startIdx ))
                            {
                                NavigationSystem.NavigationStart( this , startIdx , naviEndInfo_.unitIdx_ , nextGrid.GetID());
                            }
                            else
                            {
                                isNavFinish = true;
                            }
                        }
                        else
                        {
                            if(    nextGrid.GetExitIdx( naviEndInfo_.grid_ , out Vector2Int endIdx ) 
                                && nextGrid.GetExitIdx( nowGrid , out Vector2Int startIdx ))
                            {
                                NavigationSystem.NavigationStart( this , startIdx , endIdx , nextGrid.GetID());
                            }
                            else
                            {
                                isNavFinish = true;
                            }
                        }
                    }
                    else
                    {
                        isNavFinish = true;
                    }
                }
                else
                {
                    isNavFinish = true;
                }
            }

            if( isNavFinish )
            {
                move_.MoveStart();
            }
        }

        /// <summary>
        /// アクターを作成
        /// </summary>
        public static NavigationActor CreateNavigationActor( Vector2Int _size , NavigationSystem.NAVIGATION_ROUTE_TYPE _routeType , List<NavigationGrid.UNIT_TYPE> _blockList , params NavigationGrid[] _naviGrid )
        {
            if( _naviGrid.Length == 0 )
            {
                Debug.LogError( "NaviGrid is Empty" );
                return null;
            }

            GameObject actorGO = new GameObject( "NaviActor" );

            //位置を設定
            actorGO.transform.position = _naviGrid[0].GetUnitCenterPosition( Vector2Int.zero );

            var actor = actorGO.AddComponent<NavigationActor>();

            //グリッドを設定
            actor.naviGrid_ = new Dictionary<int, NavigationGrid>();
            foreach( var grid in _naviGrid )
            {
                if( !actor.naviGrid_.ContainsKey( grid.GetID()))
                {
                    actor.naviGrid_.Add( grid.GetID() , grid );
                }
            }
            
            //サイズと移動速度を設定
            actor.size_ = _size;      
            const float moveSpeed = ACTOR_MOVE_SPEED;
            actor.move_ = new Move( actor , moveSpeed );

            //経路タイプを設定
            actor.routeType_ = _routeType;

            //障害物を設定
            if( _blockList != null && _blockList.Count > 0 )
            {
                actor.blockList_ = new int[_blockList.Count];
                for( int nCnt = 0 ; nCnt < _blockList.Count ; nCnt++ )
                {
                    actor.blockList_[ nCnt ] = (int)_blockList[ nCnt ];
                }
            }
            
            //描画
            {
                float unitLength = _naviGrid[0].GetUnitLength();

                Color color = new Color( 0.9f , 0.8f , 0.2f , 0.5f );
                MeshRender.RenderSquare( actorGO ,
                                         Vector3.zero ,
                                         Vector2.one * unitLength ,
                                         color );

                MeshRender.RenderSquare( actorGO ,
                                         new Vector3( unitLength * ( _size.x - 1 ) * 0.5f , unitLength * ( _size.y - 1 ) * 0.5f , 0.0f ) ,
                                         new Vector2( _size.x * unitLength , _size.y * unitLength ) ,
                                         color );
            }

            return actor;
        }
    }
}

