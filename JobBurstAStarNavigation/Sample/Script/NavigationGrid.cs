using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// グリッド
    /// </summary>
    public class NavigationGrid : MonoBehaviour
    {
        /// <summary>
        /// ユニットタイプ
        /// </summary>
        public enum UNIT_TYPE
        {
            NONE  = 0,
            STONE = 1,  //石
            WATER = 2,  //水
        }

        /// <summary>
        /// ユニット描画
        /// </summary>
        public struct UnitRender
        {
            public MeshFilter mesh_;
            public TextMesh  text_;

            public void Clear()
            {
                if( mesh_ != null ){ Destroy( mesh_.gameObject );}
                if( text_ != null ){ Destroy( text_.gameObject );}
            }
        }

        //背景色
        private Color bgColor_;

        //グリッド行数
        private int rowNum_;

        //グリッド列数
        private int colNum_;

        //ユニット長さ
        private float unitLength_;

        //ID
        private int id_;

        //グリッド情報
        private NavigationSystem.UnitInfo[,] gridInfo_;

        //グリッド中心位置
        private Vector3 center_;

        //ユニット描画
        private Dictionary<UNIT_TYPE, List<UnitRender>> unitRender_;

        //ノータイプユニットのインデックス
        private List<int> noneTypeUnitIdx_;

        //出口
        private Dictionary< NavigationGrid , Vector2Int > exitArray_;

        /// <summary>
        /// グリッド描画
        /// </summary>
        private void DrawRender()
        {
            int squareOrder = -20;
            MeshRender.RenderSquare( this.gameObject,
                                     center_,
                                     new Vector2( unitLength_ * colNum_, unitLength_ * rowNum_ ),
                                     bgColor_ ,
                                     squareOrder );
            
            int lineOrder = -10;
            float halfUnitLength = unitLength_ * 0.5f;
            for( int nCnt = 0 ; nCnt < colNum_ + 1 ; nCnt++ )
            {
                float xPos   = center_.x - colNum_ * halfUnitLength + nCnt * unitLength_;
                float yStart = center_.y - rowNum_ * halfUnitLength;
                float yEnd   = center_.y + rowNum_ * halfUnitLength;
                MeshRender.RenderLine( this.gameObject,
                                       new Vector2( xPos , yStart ),
                                       new Vector2( xPos , yEnd ),
                                       Color.black,
                                       lineOrder );
            }

            for( int nCnt = 0 ; nCnt < rowNum_ + 1 ; nCnt++ )
            {
                float yPos   = center_.y - rowNum_ * halfUnitLength + nCnt * unitLength_;
                float xStart = center_.x - colNum_ * halfUnitLength;
                float xEnd   = center_.x + colNum_ * halfUnitLength;

                MeshRender.RenderLine( this.gameObject,
                                       new Vector2( xStart , yPos ),
                                       new Vector2( xEnd , yPos ),
                                       Color.black,
                                       lineOrder );
            }

            /*
            for( int nRow = 0 ; nRow < rowNum_ ; nRow++ )
            {
                for( int nCol = 0 ; nCol < colNum_ ; nCol++ )
                {
                    Vector2 pos = GetUnitCenterPosition( new Vector2Int( nCol , nRow ));
                    MeshRender.RenderText( this.gameObject,
                                           nRow + "," + nCol,
                                           2.5f,
                                           pos,
                                           Color.black,
                                           -5);
                }
            }
            */
        }

        /// <summary>
        /// 破棄
        /// </summary>
        private void OnDestroy()
        {
            InfoClear();
        }

        /// <summary>
        /// 情報クリア
        /// </summary>
        public void InfoClear()
        {
            noneTypeUnitIdx_.Clear();
            for( int nRow = 0 , nCnt = 0; nRow < rowNum_ ; nRow++ )
            {
                for( int nCol = 0 ; nCol < colNum_ ; nCol++ , nCnt ++ )
                {
                    gridInfo_[ nRow , nCol ].Clear();
                    noneTypeUnitIdx_.Add( nCnt );    
                }
            }        
            foreach( var renderType in unitRender_ )
            {
                foreach( var render in renderType.Value )
                {
                    render.Clear();
                }
                renderType.Value.Clear();
            }
            unitRender_.Clear();
        }

        /// <summary>
        /// タイプ指定、ユニットをランダム設定
        /// </summary>
        public void RmSetUnitByType( UNIT_TYPE _type , int _rmTypeNum )
        {
            int count = _rmTypeNum;
            while( count > 0 && noneTypeUnitIdx_.Count > 0 )
            {
                int rmIdx = Random.Range( 0 , noneTypeUnitIdx_.Count );
                Vector2Int unitIdx = OneToTwo( noneTypeUnitIdx_[ rmIdx ]);
                if( IsValidUnitIdx( unitIdx ))
                {
                    SetUnitInfo( unitIdx , new NavigationSystem.UnitInfo(( int )_type ));
                    noneTypeUnitIdx_.RemoveAt( rmIdx );
                }
                count--;
            };
        }

        /// <summary>
        /// ユニットを設定
        /// </summary>
        public void SetUnitInfo( Vector2Int _unitIdx , NavigationSystem.UnitInfo _info )
        {
            gridInfo_[ _unitIdx.y , _unitIdx.x ] = _info;
            CreateUnitGO(_unitIdx, _info );
        }

        /// <summary>
        /// ユニットGOを生成
        /// </summary>
        private void CreateUnitGO( Vector2Int _unitIdx , NavigationSystem.UnitInfo _info )
        {
            UNIT_TYPE unitType = ( UNIT_TYPE )_info.unitType_;

            //メッシュ作成
            MeshFilter renderMesh = null;
            {
                Color color = default;
                switch( unitType )
                {
                    case UNIT_TYPE.STONE:
                        color = Color.gray;
                        break;
                    case UNIT_TYPE.WATER:
                        color = new Color( 0.08f , 0.68f , 0.85f , 1.0f );
                        break;
                }

                //ウェイトが設定されたユニットが見た目分かりやすくなるため、ちょっと濃い色を利用
                if( _info.unitWeight_ > 0 )
                {
                    color *= 0.8f;
                    color.a = 1.0f;
                }

                renderMesh = MeshRender.RenderSquare( this.gameObject,
                                                      GetUnitCenterPosition( _unitIdx ),
                                                      Vector2.one * unitLength_,
                                                      color,
                                                      -15 );
            }

            //テキスト作成
            TextMesh renderText = null;
            {
                string renderTextContent = null;
                switch( unitType )
                {
                    case UNIT_TYPE.WATER:
                    {
                        renderTextContent = "WATER";
                    }
                    break;
                    case UNIT_TYPE.STONE:
                    {
                        renderTextContent = "STONE";
                    }
                    break;
                    default:
                    break;
                }

                if( renderTextContent != null )
                {
                    renderText = MeshRender.RenderText( this.gameObject ,
                                                        renderTextContent ,
                                                        1.7f ,
                                                        GetUnitCenterPosition( _unitIdx ) , 
                                                        Color.black ,
                                                        -15 );
                }
            }

            if( !unitRender_.ContainsKey( unitType )){ unitRender_.Add( unitType , new List<UnitRender>());}

            UnitRender render;
            render.mesh_ = renderMesh;
            render.text_ = renderText;
            unitRender_[ unitType ].Add( render );
        }

        /// <summary>
        /// 一次元を二次元に転換
        /// </summary>
        private Vector2Int OneToTwo( int _one )
        {
            return new Vector2Int( _one % colNum_ , _one / colNum_ );
        }

        /// <summary>
        /// 二次元を一次元に転換
        /// </summary>
        private int TwoToOne( Vector2Int _xy )
        {
            return _xy.y * colNum_ + _xy.x;
        }

        /// <summary>
        /// インデックスの有効判断
        /// </summary>
        private bool IsValidUnitIdx( Vector2Int _unitIdx )
        {
            return    _unitIdx.y < rowNum_ 
                   && _unitIdx.y >= 0
                   && _unitIdx.x < colNum_
                   && _unitIdx.x >= 0;
        }

        /// <summary>
        /// ユニットの中心座標を取得
        /// </summary>
        public Vector3 GetUnitCenterPosition( Vector2Int _unitIdx )
        {
            float halfLength = unitLength_ * 0.5f;
            return new Vector3( center_.x - colNum_ * halfLength + _unitIdx.x * unitLength_ + halfLength,
                                center_.y - rowNum_ * halfLength + _unitIdx.y * unitLength_ + halfLength,
                                0.0f);
        }

        /// <summary>
        ///　座標からユニットのインデックスを取得
        /// </summary>
        public bool GetUnitIdxFromPosition( out Vector2Int _unitIdx , Vector3 _position )
        {
            Vector3 ldPos = new Vector3( center_.x - colNum_ * unitLength_ * 0.5f ,
                                         center_.y - rowNum_ * unitLength_ * 0.5f , 
                                         0.0f );

            float x = ( _position.x - ldPos.x ) / unitLength_;
            float y = ( _position.y - ldPos.y ) / unitLength_;
            if( x >= 0 && y >= 0 )
            {
                Vector2Int idx = new Vector2Int((int)x, (int)y);
                if (IsValidUnitIdx( idx ))
                {
                    _unitIdx = idx;
                    return true;
                }
            }        
            _unitIdx = Vector2Int.zero;
            return false;
        }

        /// <summary>
        /// グリッド情報を取得
        /// </summary>
        public NavigationSystem.UnitInfo[,] GetGridInfo(){ return gridInfo_;}

        /// <summary>
        /// ユニット長さを取得
        /// </summary>
        public float GetUnitLength(){ return unitLength_;}

        /// <summary>
        /// グリッドIDを取得
        /// </summary>
        public int GetID(){ return id_;}

        /// <summary>
        /// 出口を設定
        /// ※マルチグリッドの場合のみ利用
        /// </summary>
        public void SetExit( NavigationGrid _exitGrid , Vector2Int _exitIdx )
        {
            if( !exitArray_.ContainsKey( _exitGrid ))
            {
                exitArray_.Add( _exitGrid , _exitIdx );
            }
            else
            {
                exitArray_[ _exitGrid ] = _exitIdx;
            }
        }

        /// <summary>
        /// 次のグリッドを取得
        /// ※マルチグリッドの場合のみ利用
        /// </summary>
        public NavigationGrid GetNextGrid( NavigationGrid _exitGrid )
        {
            //最短経路
            int countMin = -1;
            NavigationGrid nextGrid = null;
            foreach( var exit in exitArray_ )
            {
                if( exit.Key == _exitGrid )
                {
                    nextGrid = exit.Key;
                    break;
                }

                int count = GetGridCountToEnd( exit.Key , _exitGrid );
                if( count >= 0 )
                {
                    if( count < countMin || countMin < 0 )
                    {
                        countMin = count;
                        nextGrid = exit.Key;
                    }
                }
            }
            return nextGrid;
        }

        /// <summary>
        /// 最後のグリッドまで経過したグリッドのカウント数を取得
        /// ※マルチグリッドの場合のみ利用
        /// </summary>
        private int GetGridCountToEnd( NavigationGrid _grid , NavigationGrid _exitGrid , NavigationGrid _prevGrid = null , int _count = 0 )
        {
            foreach( var exit in _grid.exitArray_ )
            {
                if( exit.Key == _exitGrid )
                {
                    return _count;
                }
                else
                {
                    if( exit.Key != _grid && exit.Key != _prevGrid )
                    {              
                        int count = GetGridCountToEnd( exit.Key , _exitGrid , _grid , ++_count );
                        if( count >= 0 )
                        {
                            return count;
                        }
                    }
                }                
            }
            return -1;            
        }

        /// <summary>
        /// 出口のインデックスを取得
        /// ※マルチグリッドの場合のみ利用
        /// </summary>
        public bool GetExitIdx( NavigationGrid _exitGrid , out Vector2Int _exitIdx )
        {
            var grid = GetNextGrid( _exitGrid );
            if( grid != null )
            {
                return exitArray_.TryGetValue( grid , out _exitIdx );
            }
            _exitIdx = Vector2Int.zero;
            return false;            
        }

        /// <summary>
        /// 作成
        /// </summary>
        public static NavigationGrid Create( Vector2 _center, int _rowNum, int _colNum, float _unitLength , int _id , Color _bgColor = default )
        {
            GameObject gridGO   = new GameObject("NavigationGrid_" + _id );
            var grid            = gridGO.AddComponent<NavigationGrid>();
            grid.center_        = _center;
            grid.unitLength_    = _unitLength;
            grid.rowNum_        = _rowNum;
            grid.colNum_        = _colNum;
            grid.id_            = _id;
            grid.bgColor_       = _bgColor == Color.clear ? new Color(0.2f, 0.85f, 0.65f, 0.5f) : _bgColor;

            grid.noneTypeUnitIdx_ = new List<int>( _rowNum * _colNum );
            grid.unitRender_      = new Dictionary<UNIT_TYPE, List<UnitRender>>();
            grid.gridInfo_        = new NavigationSystem.UnitInfo[ _rowNum , _colNum ];
            grid.exitArray_       = new Dictionary<NavigationGrid, Vector2Int>();

            //情報クリア
            grid.InfoClear();

            //グリッド描画
            grid.DrawRender();

            grid.enabled = false;

            return grid;
        }
    }
}