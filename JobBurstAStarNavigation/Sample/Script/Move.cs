using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// 移動処理
    /// </summary>
    public class Move
    {
        private MonoBehaviour owner_;       //オーナー

        private float moveSpeed_;           //速度  
        private float moveTime_;            //時間   
        private float moveCount_;           //カウント
        private int   moveStep_;            //ステップ 
    
        private Vector3 moveStart_;         //開始位置
        private Vector3 moveEnd_;           //終了位置

        private List<Vector3> moveRoute_;   //経路

        private bool isMoveStart_;          //開始フラグ

        public Move( MonoBehaviour _owner , float _moveSpeed )
        {
            owner_       = _owner;
            moveSpeed_   = _moveSpeed;
            moveStep_    = 0;
            isMoveStart_ = false;
            moveRoute_   = new List<Vector3>();  
        }

        /// <summary>
        /// 移動更新
        /// </summary>
        public void MoveUpdate( float _dt )
        {
            if( owner_ == null ) return;
            switch( moveStep_ )
            {
                case 0:
                {
                    if( moveRoute_.Count > 1 && isMoveStart_ )
                    {
                        isMoveStart_ = false;                    
                        moveStep_ = 1;
                    }
                }
                break;
                case 1:
                {
                    if( moveRoute_.Count > 1 )
                    {
                        moveStart_  = moveRoute_[ 0 ];
                        moveEnd_    = moveRoute_[ 1 ];
                        moveCount_  = 0.0f;
                        moveTime_   = ( moveEnd_ - moveStart_ ).magnitude / moveSpeed_;
                        moveRoute_.RemoveAt( 0 );
                        moveStep_ = 2;
                    }
                    else
                    {
                        MoveStop();
                    }
                }
                break;
                case 2:
                {
                    if( moveCount_ >= moveTime_ )
                    {
                        moveCount_ = moveTime_;
                        owner_.transform.position = moveEnd_;
                        moveStep_ = 1;
                    }
                    else
                    {
                        moveCount_ += _dt;
                        owner_.transform.position = moveStart_ + ( moveEnd_ - moveStart_ ) * ( moveCount_ / moveTime_ );
                    }
                }
                break;           
            }
        }

        /// <summary>
        /// 移動停止
        /// </summary>
        public void MoveStop()
        {
            moveRoute_.Clear();
            moveStep_ = 0;
        }

        /// <summary>
        /// 移動開始
        /// </summary>
        public void MoveStart()
        {
            if( moveStep_ == 0 )
            {
                isMoveStart_ = true;
            }
        }

        /// <summary>
        /// ノートを経路に追加
        /// </summary>
        public void AddToMoveRoute( Vector3 _routeNote )
        {
            moveRoute_.Add( _routeNote );
        }
    }
}

