[![license](https://img.shields.io/badge/license-MIT-brightgreen.svg?style=flat-square)](https://github.com/YuloongBY/JobBurstAStarNavigation/blob/main/LICENSE)

# JobBurstAStarNavigation
 JobSystemとBurstCompilerを利用するマルチスレッドA*ナビゲーション
 
 Multi-threaded A* navigation based on JobSystem and BurstCompiler
 
## 特徴
### ・マルチスレッド処理にJobSystemを利用したので、スレッドセーフが確保できる 
　    JobSystemとは：https://docs.unity3d.com/ja/2018.4/Manual/JobSystem.html
 
### ・BurstCompilerを利用したので、ナビゲーション処理が高速化になる
　    BurstCompilerとは：https://docs.unity3d.com/Packages/com.unity.burst@0.2-preview.20/manual/index.html
      
      ※ 自分検証した結果、利用しないより、処理速度が約5倍速くなる。
      
      
      
```diff
- BurstCompilerを利用するために、「Burst」パッケージが必要なので、
- パッケージをプロジェクトに導入した後、NavigationSystem.csの24行目を有効にしてください。    
```

```csharp
  24    //#define USING_BURST
```

### ・ナビゲーションを利用するオブジェクトのサイズが任意に設定できる

　「サイズ」パラメータをナビゲーション計算に組み込んで、すべてのオブジェクトがナビゲーションシステムに活用できる。

![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviSize0.gif)
![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviSize1.gif)
![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviSize2.gif)

### ・ナビゲーションを利用するオブジェクトによって、障害物が別々設定できる

　障害物情報をグリッド側からオブジェクト側が持つように変更、ナビゲーション計算する時、動的に差し替える。

「灰色」が障害物になった場合　　　　　　　　　　

![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviBlock0.gif)

「水色」、「灰色」が障害物になった場合

![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviBlock1.gif)
 
### ・ナビゲーションで計算したルートを「4方向」、「8方向」、「任意」3種類が選択できる
 
　    ※「4方向」、「8方向」のルートが合理的な路線に見えるため、「ブレゼンハムのアルゴリズム」で最適化した。
     
「任意」：方向の制限が特にない

![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviRouteType0.gif)

「4方向」：上下左右の4方向

![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviRouteType1.gif)

「8方向」：上下左右と斜めの8方向

![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviRouteType2.gif)
  
### ・ユニットにウェイトが設定できる

　ウェイト値をユニットのG値に加算した後、最終G値にとして計算する。

濃い青色ユニットにウェイトを付けたので、探索終点は真ん中の「水路」ところ以外の場合、「水路」に入れない。

![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviWeight.gif)

### ・複数グリッドの連続ナビゲーション処理を対応

　グリッドの複雑度が高ければ高いほど処理が重くなるので、処理負荷を軽減するため、グリッドを複数に分割して計算する。
　
 
![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviMulti.gif)

### ・探索終点まで行けない場合、最短経路が取得できる

　終点まで行けない場合、計算されたユニット中にH値最小のユニットを終点にとして出力する。

![Image](https://github.com/YuloongBY/BYImage/blob/main/JobBurstAStarNavigation/naviDeadEnd.gif)

### ・効率アップのため、無駄な計算しない
　
 始点と終点の間に障害物があるかどうか事前に判断して、障害物ない場合、ナビゲーション処理を行わなく、始点と終点をこのまま結果にとして出力する。

### ・シングルトンでまとめ管理するので、使いやすくなる
 
## 使い方
パッケージ中のサンプルを参考にしてください。

## API

### Static Methods
```csharp
/// <summary>
/// 生成
/// ※シングルトンなので、最初一回だけ呼べば良い
/// </summary>
NavigationSystem.Create();
```

```csharp
/// <summary>
/// グリッドを導入
/// ※メモリ割り当て処理発生するので、注意してください。
/// </summary>
/// <param name="_gridInfo">  グリッド情報 </param>
/// <param name="_gridID">    グリッドID(マルチグリッドの場合だけ設定必要) </param>
/// <param name="_threadNum"> 同時発生最大スレッド数 </param>
NavigationSystem.ImportGrid( UnitInfo[,] _gridInfo , int _gridID = 0 , int _threadNum = 5 );
```

```csharp
/// <summary>
/// 登録
/// ※ナビゲーション機能を利用する前に、利用者がこの処理でナビゲーションシステムに登録してください。
/// </summary>
/// <param name="_register">       登録者 </param>
/// <param name="_registerSize">   登録者サイズ </param>
/// <param name="_routeType">      経路タイプ </param>    
/// <param name="_finishCallback"> 完成した時のコールバック </param>
/// <param name="_blockType">      障害物タイプ </param>
NavigationSystem.Register( MonoBehaviour _register , Vector2Int _registerSize , NAVIGATION_ROUTE_TYPE _routeType , NAVIGATION_FINISH_CALLBACK _finishCallback , params int[] _blockType );
```

```csharp
/// <summary>
/// 登録解除
/// ※利用者を破棄、ナビゲーション機能を使わなくなる場合、この処理でナビゲーションシステムから登録を解除してください。
/// </summary>
/// <param name="_register"> 登録者 </param>
NavigationSystem.Unregister( MonoBehaviour _register );
```

```csharp
/// <summary>
/// 登録者のサイズを設定
/// </summary>
/// <param name="_register">     登録者 </param>
/// <param name="_registerSize"> 登録者サイズ </param>
NavigationSystem.SetSize( MonoBehaviour _register , Vector2Int _registerSize )
```

```csharp
/// <summary>
/// 登録者の経路タイプを設定
/// </summary>
/// <param name="_register">  登録者 </param>
/// <param name="_routeType"> 経路タイプ </param>
NavigationSystem.SetRouteType( MonoBehaviour _register , NAVIGATION_ROUTE_TYPE _routeType )
```

```csharp
/// <summary>
/// 登録者のブロックタイプを設定
/// </summary>
/// <param name="_register">  登録者 </param>
/// <param name="_blockType"> ブロックタイプ </param>
NavigationSystem.SetBlockType( MonoBehaviour _register , params int[] _blockType )
```

```csharp
/// <summary>
/// ナビゲーション開始
/// </summary>
/// <param name="_register">          登録者 </param>
/// <param name="_startIdx">          開始インデックス </param>
/// <param name="_endIdx">            終了インデックス </param>
/// <param name="_gridID">            グリッドID(マルチグリッドの場合だけ設定必要) </param>
/// <param name="_temFinishCallback"> 完了した時の一時コールバック </param>
NavigationSystem.NavigationStart( MonoBehaviour _register , Vector2Int _startIdx , Vector2Int _endIdx , int _gridID = 0 , NAVIGATION_FINISH_CALLBACK _temFinishCallback = null );
```

```csharp
/// <summary>
/// ユニット情報を差し替え
/// ※グリッドサイズ変わらず、グリッド中のユニット情報だけ変更したい場合、利用してください。
/// </summary>
/// <param name="_unitIdx">  ユニットインデックス </param>
/// <param name="_unitInfo"> ユニット情報 </param>
/// <param name="_gridID">   グリッドID(マルチグリッドの場合だけ設定必要) </param>
NavigationSystem.ReplaceUnitInfo( Vector2Int _unitIdx , UnitInfo _unitInfo , int _gridID = 0 );
```

```csharp
/// <summary>
/// グリッド全体を差し替え
/// ※グリッドが変更したい場合、利用する
/// ※メモリ割り当て処理発生するので、注意してください。
/// </summary>
/// <param name="_gridInfo"> グリッド情報 </param>
/// <param name="_gridID">   グリッドID(マルチグリッドの場合だけ設定必要) </param>
NavigationSystem.ReplaceGridInfo( UnitInfo[,] _gridInfo , int _gridID = 0 );
```

```csharp
/// <summary>
/// ナビゲーション情報をすべて削除
/// ※ステージ遷移の場合、利用することが多い
/// </summary>
NavigationSystem.AllClear();
```

```csharp
/// <summary>
/// 強制破棄
/// </summary>
NavigationSystem.ForceDestroy();
```

### Public Struct / Enum / Delegate
```csharp
/// <summary>
/// ユニット情報
/// </summary>
public struct UnitInfo
{
    //タイプ
    public int unitType_;
    //ウエイト
    public int unitWeight_;
}
```

```csharp
/// <summary>
/// 経路タイプ
/// </summary>
public enum NAVIGATION_ROUTE_TYPE
{
    FOUR_DIRECTION = 0 ,  //4方向
    EIGHT_DIRECTION    ,  //8方向
    FREE_DIRECTION     ,  //任意方向
}
```

```csharp
/// <summary>
/// ナビゲーション完成した時のコールバック
/// </summary>
/// <param name="_route">経路</param>
/// <param name="_result">結果</param>
/// <param name="_gridID">グリッドID</param>
/// <param name="_isSameLast">前回と同じかどうか</param>
public delegate void NAVIGATION_FINISH_CALLBACK( List<Vector2Int> _route , NAVIGATION_RESULT _result , int _gridID , bool _isSameLast );
```
