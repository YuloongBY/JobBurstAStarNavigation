using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// サンプル
    /// </summary>
    public class NavigationSample : MonoBehaviour
    {
        //ユニット最小値
        private const int GRID_UNIT_NUM_MIN = 10;
        //ユニット最大値
        private const int GRID_UNIT_NUM_MAX = 20;
        //ユニット長さ
        private const float GRID_UNIT_LENGTH = 75.0f;
        //アクターサイズ最小値
        private const int ACTOR_SIZE_MIN = 1;
        //アクターサイズ最大値
        private const int ACTOR_SIZE_MAX = 3;
        //障害物数の最小値
        private const int ONE_BLOCK_NUM_MIN = 5;
        //障害物数の最大値
        private const int ONE_BLOCK_NUM_MAX = 20;
        
        //アクターの幅
        private int actorWeight_ = ACTOR_SIZE_MIN;
        //アクターの長さ
        private int actorHeight_ = ACTOR_SIZE_MIN;

        //アクターの経路タイプ
        private NavigationSystem.NAVIGATION_ROUTE_TYPE actorRouteType_ = NavigationSystem.NAVIGATION_ROUTE_TYPE.FREE_DIRECTION;

        //石の数
        private int stoneNum_ = 10;
        //水の数
        private int waterNum_ = 10;

        //石を無視
        private bool isIgnoreStone_ = false;
        //水を無視
        private bool isIgnoreWater_ = true;

        //アクター
        NavigationActor actor_ = null;

        //グリッド
        List<NavigationGrid> gridArray_ = null;

        private void Awake()
        {
            gridArray_ = new List<NavigationGrid>();
        }

        void Start()
        {
            //ナビゲーションシステム作成
            NavigationSystem.Create();
            
            //グリッドを作成
            CreateGrid();
            //障害物リセット
            ResetBlock();
            //アクターリセット
            ResetActor();           
        }

        /// <summary>
        /// 更新
        /// </summary>
        void Update()
        {
            //クリックした位置にナビゲーションで経路を計算し、移動させる
            if( Input.GetMouseButtonDown( 0 ))
            {
                if( actor_ != null )
                {                    
                    actor_.NavigationStart( Camera.main.ScreenToWorldPoint( Input.mousePosition ));
                }
            }
        }

        /// <summary>
        /// GUI描画
        /// </summary>
        void OnGUI()
        {
            float guiWidthRate =  Screen.width / 1920.0f;
            float guiHeightRate = Screen.height / 1080.0f;

            int guiWidth     = ( int )( 200 * guiWidthRate );
            int guiHeight    = ( int )( 50  * guiHeightRate );
            int guiXLocation = Screen.width - guiWidth;
            int guiYLocation = 0;
            int guiYInterval = ( int )( 10 * guiHeightRate );

            int smallBtnFontSize = ( int )( 20 * guiHeightRate );
            int btnFontSize = ( int )( 25 * guiHeightRate );            
            GUIStyle btnStyle = new GUIStyle( GUI.skin.button );
            btnStyle.fontSize = btnFontSize;

            int lblFontSize = ( int )( 22 * guiHeightRate );            
            GUIStyle lblStyle = new GUIStyle( GUI.skin.label );
            lblStyle.fontSize = lblFontSize;

            int togFontSize = ( int )( 22 * guiHeightRate );                        
            GUIStyle togStyle = new GUIStyle( GUI.skin.toggle );
            togStyle.fontSize = togFontSize;

            //ランダムでグリッドをリセット
            {                
                if( GUI.Button( new Rect( guiXLocation , guiYLocation , guiWidth , guiHeight ) , "Random Grid" , btnStyle ))
                {
                    isLockGrid_ = false;
                    ClearGrid();
                    CreateGrid();
                    ResetBlock();
                    ResetActor();
                }

                if( !isLockGrid_ )
                {
                    //ランダムで障害物をリセット
                 
                    bool isResetBlock = false;
                    
                    guiYLocation += guiHeight;
                    btnStyle.fontSize = smallBtnFontSize;
                    if( GUI.Button( new Rect( guiXLocation , guiYLocation , guiWidth * 0.8f , guiHeight * 0.8f ) , "Random Block" , btnStyle ))
                    {
                        isResetBlock = true;
                    }
                    btnStyle.fontSize = btnFontSize;  

                    //石の数を調整
                    guiYLocation += guiHeight - guiYInterval;
                    GUI.Label( new Rect( guiXLocation , guiYLocation , guiWidth , guiHeight ) , string.Format( "Stone: {0}" , stoneNum_ ) , lblStyle );
                    guiYLocation += guiHeight - guiYInterval * 2;
                    
                    int newStoneNum_ = Mathf.RoundToInt( GUI.HorizontalSlider( new Rect( guiXLocation , guiYLocation , guiWidth * 0.8f , guiHeight * 0.5f ), stoneNum_ , ONE_BLOCK_NUM_MIN, ONE_BLOCK_NUM_MAX ));
                    if( newStoneNum_ != stoneNum_ )
                    {
                        stoneNum_ = newStoneNum_;
                        isResetBlock = true;
                    }

                    //水の数を調整
                    guiYLocation += guiHeight - guiYInterval * 4;
                    GUI.Label( new Rect( guiXLocation , guiYLocation , guiWidth , guiHeight ) , string.Format( "Water: {0}" , waterNum_ ) , lblStyle );
                    guiYLocation += guiHeight - guiYInterval * 2;
                   
                    int newWaterNum_ = Mathf.RoundToInt( GUI.HorizontalSlider( new Rect( guiXLocation , guiYLocation , guiWidth * 0.8f , guiHeight * 0.5f ), waterNum_ , ONE_BLOCK_NUM_MIN, ONE_BLOCK_NUM_MAX ));                
                    if( newWaterNum_ != waterNum_ )
                    {
                        waterNum_ = newWaterNum_;
                        isResetBlock = true;
                    }

                    if( isResetBlock )
                    {
                        ResetBlock();
                    }
                }
            }
            
            //アクターをリセット
            {
                bool isResetActor = false;

                guiYLocation += guiHeight + guiYInterval * 2;
                if( GUI.Button( new Rect( guiXLocation , guiYLocation , guiWidth , guiHeight ) , "Reset Actor" , btnStyle ))
                {
                    isResetActor = true;
                }

                //経路タイプを切り替え
                {
                    string btnText = null;
                    switch ( actorRouteType_ )
                    {
                        case NavigationSystem.NAVIGATION_ROUTE_TYPE.FOUR_DIRECTION:
                        btnText = "4 Direction";
                        break;
                        case NavigationSystem.NAVIGATION_ROUTE_TYPE.EIGHT_DIRECTION:
                        btnText = "8 Direction";
                        break;
                        case NavigationSystem.NAVIGATION_ROUTE_TYPE.FREE_DIRECTION:
                        btnText = "Free Direction";
                        break;
                    }

                    guiYLocation += guiHeight;
                    btnStyle.fontSize = smallBtnFontSize;
                    if( GUI.Button( new Rect( guiXLocation , guiYLocation , guiWidth * 0.8f , guiHeight * 0.8f ) , btnText , btnStyle ))
                    {
                        int typeMax = 3;
                        int typeInt = (int)actorRouteType_;
                        typeInt++;
                        typeInt %= typeMax;
                        actorRouteType_ = ( NavigationSystem.NAVIGATION_ROUTE_TYPE )typeInt;
                        isResetActor = true;                        
                    }
                    btnStyle.fontSize = btnFontSize; 
                }

                //アクターサイズを調整
                {
                    guiYLocation += guiHeight + guiYInterval;
                    GUI.Label( new Rect( guiXLocation , guiYLocation , guiWidth , guiHeight ) , string.Format( "ActorWeight: {0}" , actorWeight_ ) , lblStyle );
                    guiYLocation += guiHeight - guiYInterval * 2;
                
                    int newActorWeight = Mathf.RoundToInt( GUI.HorizontalSlider(new Rect( guiXLocation , guiYLocation , guiWidth * 0.8f , guiHeight * 0.5f ), actorWeight_ , ACTOR_SIZE_MIN , ACTOR_SIZE_MAX ));
                    if( newActorWeight != actorWeight_ )
                    {
                        actorWeight_ = newActorWeight;
                        isResetActor = true;
                    }

                    guiYLocation += guiHeight - guiYInterval * 4;
                    GUI.Label( new Rect( guiXLocation , guiYLocation , guiWidth , guiHeight ) , string.Format( "ActorHeight: {0}" , actorHeight_ ) , lblStyle );
                    guiYLocation += guiHeight - guiYInterval * 2;
                
                    int newActorHeight = Mathf.RoundToInt( GUI.HorizontalSlider(new Rect( guiXLocation , guiYLocation , guiWidth * 0.8f , guiHeight * 0.5f ), actorHeight_ , ACTOR_SIZE_MIN , ACTOR_SIZE_MAX ));
                    if( newActorHeight != actorHeight_ )
                    {
                        actorHeight_ = newActorHeight;
                        isResetActor = true;
                    }
                }

                //障害物調整
                {
                    //石無視？
                    guiYLocation += guiHeight - guiYInterval * 2;
                
                    bool temIsIgnoreStone_ = GUI.Toggle( new Rect( guiXLocation , guiYLocation , guiWidth * 0.8f, guiHeight * 0.5f ) , isIgnoreStone_ , "Ignore Stone" , togStyle );
                    if( temIsIgnoreStone_ != isIgnoreStone_ )
                    {
                        isIgnoreStone_ = temIsIgnoreStone_;
                        isResetActor = true;
                    }

                    //水無視？
                    guiYLocation += guiHeight - guiYInterval;

                    bool temIsIgnoreWater = GUI.Toggle( new Rect( guiXLocation , guiYLocation , guiWidth * 0.8f, guiHeight * 0.5f ) , isIgnoreWater_ , "Ignore Water" , togStyle );
                    if( temIsIgnoreWater != isIgnoreWater_ )
                    {
                        isIgnoreWater_ = temIsIgnoreWater;
                        isResetActor = true;
                    }
                }

                if( isResetActor )
                {
                    ResetActor();                
                }
            }

            //ウエイトテストグリッド
            guiYLocation += guiHeight + guiYInterval * 2;
            if( GUI.Button( new Rect( guiXLocation , guiYLocation , guiWidth , guiHeight ) , "WeightTest Grid" , btnStyle ))
            {
                isLockGrid_    = true;
                isIgnoreStone_ = false;
                isIgnoreWater_ = true;
                actorWeight_   = 1;
                actorHeight_   = 1;
                ClearGrid();
                CreateWeightTestGrid();                
                ResetActor();
            }

            //マルチテストグリッド
            guiYLocation += guiHeight;
            if( GUI.Button( new Rect( guiXLocation , guiYLocation , guiWidth , guiHeight ) , "MultiTest Grid" , btnStyle ))
            {
                isLockGrid_    = true;
                isIgnoreStone_ = false;
                isIgnoreWater_ = true;
                actorWeight_   = 1;
                actorHeight_   = 1;
                ClearGrid();
                CreateMultiTestGrid();
                ResetActor();
            }
        }
        bool isLockGrid_ = false;
        
        /// <summary>
        /// ウエイトテスト用グリッドを作成
        /// </summary>
        private void CreateWeightTestGrid()
        {
            //グリッド作成
            var grid = CreateGrid( 8 , 7 );

            //障害物を指定
            grid.SetUnitInfo( new Vector2Int( 2 , 2 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));           
            grid.SetUnitInfo( new Vector2Int( 3 , 2 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
            grid.SetUnitInfo( new Vector2Int( 4 , 2 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
            grid.SetUnitInfo( new Vector2Int( 5 , 2 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
            
            grid.SetUnitInfo( new Vector2Int( 2 , 4 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));           
            grid.SetUnitInfo( new Vector2Int( 3 , 4 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
            grid.SetUnitInfo( new Vector2Int( 4 , 4 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
            grid.SetUnitInfo( new Vector2Int( 5 , 4 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
            
            grid.SetUnitInfo( new Vector2Int( 3 , 3 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.WATER ));
            grid.SetUnitInfo( new Vector2Int( 4 , 3 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.WATER ));
            
            //両側の水ユニットにウェイトを付き
            var sideWaterInfo = new NavigationSystem.UnitInfo((int)NavigationGrid.UNIT_TYPE.WATER );
            sideWaterInfo.unitWeight_ = 3;

            grid.SetUnitInfo( new Vector2Int( 2 , 3 ) , sideWaterInfo );    
            grid.SetUnitInfo( new Vector2Int( 5 , 3 ) , sideWaterInfo );

            //グリッド情報を更新
            NavigationSystem.ReplaceGridInfo( grid.GetGridInfo() , grid.GetID());
        }

        /// <summary>
        /// マルチテスト用グリッドを作成
        /// </summary>
        private void CreateMultiTestGrid()
        {
            int colNum = 6;
            int rowNum = 6;

            //グリッド作成
            var grid0 = CreateGrid( colNum , rowNum , new Vector3( -GRID_UNIT_LENGTH * colNum * 0.5f , -GRID_UNIT_LENGTH * rowNum * 0.5f , 0.0f ) , 0 );
            var grid1 = CreateGrid( colNum , rowNum , new Vector3(  GRID_UNIT_LENGTH * colNum * 0.5f , -GRID_UNIT_LENGTH * rowNum * 0.5f , 0.0f ) , 1 , new Color( 1.0f , 0.0f , 0.0f , 0.5f ));
            var grid2 = CreateGrid( colNum , rowNum , new Vector3(  GRID_UNIT_LENGTH * colNum * 0.5f ,  GRID_UNIT_LENGTH * rowNum * 0.5f , 0.0f ) , 2 , new Color( 1.0f , 1.0f , 0.0f , 0.5f ));
            var grid3 = CreateGrid( colNum , rowNum , new Vector3( -GRID_UNIT_LENGTH * colNum * 0.5f ,  GRID_UNIT_LENGTH * rowNum * 0.5f , 0.0f ) , 3 , new Color( 0.0f , 1.0f , 1.0f , 0.5f ));

            //グリッド0
            {
                //障害物を指定
                grid0.SetUnitInfo( new Vector2Int( 0 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));            
                grid0.SetUnitInfo( new Vector2Int( 1 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));            
                grid0.SetUnitInfo( new Vector2Int( 2 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));                        
                grid0.SetUnitInfo( new Vector2Int( 5 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid0.SetUnitInfo( new Vector2Int( 5 , 3 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid0.SetUnitInfo( new Vector2Int( 5 , 4 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid0.SetUnitInfo( new Vector2Int( 5 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
            
                //出口を指定
                grid0.SetExit( grid1 , new Vector2Int( 5 , 1 ));
                grid0.SetExit( grid3 , new Vector2Int( 3 , 5 ));
            }

            //グリッド1
            {
                //障害物を指定
                grid1.SetUnitInfo( new Vector2Int( 0 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid1.SetUnitInfo( new Vector2Int( 0 , 3 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid1.SetUnitInfo( new Vector2Int( 0 , 4 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid1.SetUnitInfo( new Vector2Int( 0 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));                           
                grid1.SetUnitInfo( new Vector2Int( 3 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));               
                grid1.SetUnitInfo( new Vector2Int( 4 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));               
                grid1.SetUnitInfo( new Vector2Int( 5 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));                          
                //出口を指定
                grid1.SetExit( grid0 , new Vector2Int( 0 , 1 ));
                grid1.SetExit( grid2 , new Vector2Int( 1 , 5 ));
            }

            //グリッド2
            {
                //障害物を指定
                grid2.SetUnitInfo( new Vector2Int( 0 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid2.SetUnitInfo( new Vector2Int( 3 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid2.SetUnitInfo( new Vector2Int( 4 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));                           
                grid2.SetUnitInfo( new Vector2Int( 5 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));                           
                grid2.SetUnitInfo( new Vector2Int( 0 , 1 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));   
                grid2.SetUnitInfo( new Vector2Int( 0 , 2 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));   
                grid2.SetUnitInfo( new Vector2Int( 0 , 4 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));   
                grid2.SetUnitInfo( new Vector2Int( 0 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));            
                //出口を指定
                grid2.SetExit( grid1 , new Vector2Int( 1 , 0 ));
                grid2.SetExit( grid3 , new Vector2Int( 0 , 3 ));
            }

            //グリッド3
            {
                //障害物を指定
                grid3.SetUnitInfo( new Vector2Int( 0 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));           
                grid3.SetUnitInfo( new Vector2Int( 1 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid3.SetUnitInfo( new Vector2Int( 2 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid3.SetUnitInfo( new Vector2Int( 5 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid3.SetUnitInfo( new Vector2Int( 5 , 0 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid3.SetUnitInfo( new Vector2Int( 5 , 1 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid3.SetUnitInfo( new Vector2Int( 5 , 2 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid3.SetUnitInfo( new Vector2Int( 5 , 4 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                grid3.SetUnitInfo( new Vector2Int( 5 , 5 ) , new NavigationSystem.UnitInfo(( int )NavigationGrid.UNIT_TYPE.STONE ));
                //出口を指定
                grid3.SetExit( grid0 , new Vector2Int( 3 , 0 ));
                grid3.SetExit( grid2 , new Vector2Int( 5 , 3 ));
            }

            //グリッド情報を更新
            NavigationSystem.ReplaceGridInfo( grid0.GetGridInfo() , grid0.GetID());
            NavigationSystem.ReplaceGridInfo( grid1.GetGridInfo() , grid1.GetID());
            NavigationSystem.ReplaceGridInfo( grid2.GetGridInfo() , grid2.GetID());            
            NavigationSystem.ReplaceGridInfo( grid3.GetGridInfo() , grid3.GetID());
        }

        /// <summary>
        /// グリッドを削除
        /// </summary>
        private void ClearGrid()
        {
            foreach( var grid in gridArray_ )
            {
                if( grid != null )
                {
                    Destroy( grid.gameObject );            
                }
            }
            gridArray_.Clear();
            NavigationSystem.AllClear();            
        }

        /// <summary>
        /// グリッドを作成
        /// </summary>
        private NavigationGrid CreateGrid( int _unitColNum = -1 , int _unitRowNum = -1 , Vector3 _center = default , int _gridID = 0 , Color _bgColor = default )
        {       
            int unitColNum = _unitColNum;
            if( unitColNum <= 0 )
            {
                unitColNum = Random.Range( GRID_UNIT_NUM_MIN , GRID_UNIT_NUM_MAX + 1 );
            }

            int unitRowNum = _unitRowNum;
            if( unitRowNum <= 0 )
            {
                unitRowNum = Random.Range( GRID_UNIT_NUM_MIN , GRID_UNIT_NUM_MAX + 1 );
                unitRowNum = (int)( unitRowNum * 0.75f );
            }
            
            //グリッドを作成
            var grid = NavigationGrid.Create( _center , unitRowNum , unitColNum , GRID_UNIT_LENGTH , _gridID , _bgColor );            
            
            //ナビゲーションにグリッドを導入
            NavigationSystem.ImportGrid( grid.GetGridInfo() , grid.GetID());  

            gridArray_.Add( grid );

            return grid;
        }

        /// <summary>
        /// 障害物リセット
        /// </summary>
        private void ResetBlock()
        {
            foreach( var grid in gridArray_ )
            {
                if( grid != null )
                {
                    grid.InfoClear();
                    grid.RmSetUnitByType(NavigationGrid.UNIT_TYPE.STONE, stoneNum_);
                    grid.RmSetUnitByType(NavigationGrid.UNIT_TYPE.WATER, waterNum_);

                    //重置障碍物
                    NavigationSystem.ReplaceGridInfo( grid.GetGridInfo() , grid.GetID());         
                }
            }
        }

        /// <summary>
        /// アクターリセット
        /// </summary>
        private void ResetActor()
        {
            if( actor_ )
            {
                Destroy( actor_.gameObject );
                actor_ = null;
            }
            
            actorBlockList_.Clear();
            if( !isIgnoreStone_ )actorBlockList_.Add( NavigationGrid.UNIT_TYPE.STONE );
            if( !isIgnoreWater_ )actorBlockList_.Add( NavigationGrid.UNIT_TYPE.WATER );

            //アクターを作成          
            actor_ = NavigationActor.CreateNavigationActor( new Vector2Int( actorWeight_ , actorHeight_ ) , actorRouteType_ , actorBlockList_ , gridArray_.ToArray());
        }
        private List<NavigationGrid.UNIT_TYPE> actorBlockList_ = new List<NavigationGrid.UNIT_TYPE>();
    }
}
