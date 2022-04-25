using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Navigation
{
    /// <summary>
    /// メッシュ描画
    /// </summary>
    public static class MeshRender
    {
        /// <summary>
        /// ライン
        /// </summary>
        public static LineRenderer RenderLine( GameObject _parent , Vector2 _start , Vector2 _end , Color _color , int _order = 0 , float _width = 3.0f )
        {
            Vector3[] positions = new Vector3[]{ _start , _end };

            GameObject lineGo = new GameObject("Line");
            lineGo.transform.parent = _parent.transform;
            LineRenderer line = lineGo.AddComponent<LineRenderer>();
            line.positionCount = positions.Length;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = _color;
            line.endColor = _color;
            line.startWidth = _width;
            line.endWidth = _width;
            line.sortingOrder = _order;
            line.SetPositions( positions );
            return line;
        }

        /// <summary>
        /// 四角形
        /// </summary>
        public static MeshFilter RenderSquare(GameObject _parent, Vector3 _localPosition, Vector2 _size , Color _color , int _order = 0 )
        {
            //左下、左上、右上、右下
            Vector2 halfSzie = _size * 0.5f;
            Vector3[] vtxPos = new Vector3[4];
            vtxPos[0] = new Vector3( -halfSzie.x , -halfSzie.y , 0.0f );
            vtxPos[1] = new Vector3( -halfSzie.x ,  halfSzie.y , 0.0f );
            vtxPos[2] = new Vector3(  halfSzie.x ,  halfSzie.y , 0.0f );
            vtxPos[3] = new Vector3(  halfSzie.x , -halfSzie.y , 0.0f );

            GameObject meshGo = new GameObject("Square");
            meshGo.transform.parent = _parent.transform;
            meshGo.transform.localPosition = _localPosition;
        
            MeshRenderer render = meshGo.AddComponent<MeshRenderer>();
            render.sortingOrder = _order;
            render.material = new Material(Shader.Find("Sprites/Default"));
            MeshFilter meshFilter = meshGo.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.vertices = vtxPos;
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.colors = new Color[4] { _color, _color, _color, _color };
            meshFilter.mesh = mesh;
       
            return meshFilter;
        }

        /// <summary>
        /// ラウンド
        /// </summary>
        public static MeshFilter RenderRound(GameObject _parent, Vector3 _localPosition, float _radius , Color _color, int _order = 0 , int _segmentation = 15 )
        {
            Vector3[] vtxPos = new Vector3[ _segmentation + 2 ];
            Color[]   colors = new Color  [ _segmentation + 2 ];

            vtxPos[ 0 ] = Vector3.zero;
            colors[ 0 ] = _color;
            for ( int nCnt = 0 ; nCnt < _segmentation ; nCnt ++ )
            {
                float radian = Mathf.PI * 2.0f / _segmentation * nCnt;
                Vector2 point = new Vector2( _radius * Mathf.Cos( radian ) , _radius * Mathf.Sin( radian ));
                vtxPos[ nCnt + 1 ] = point;
                colors[ nCnt + 1 ] = _color;
            }
            vtxPos[ vtxPos.Length - 1 ] = vtxPos[ 1 ];
            colors[ colors.Length - 1 ] = colors[ 1 ];

            int[] triangles = new int[ _segmentation * 3 ];
            for( int nCnt = 0 ; nCnt < _segmentation; nCnt++ )
            {
                triangles[ 3 * nCnt ] = 0;
                triangles[ 3 * nCnt + 1 ] = nCnt + 1;
                triangles[ 3 * nCnt + 2 ] = nCnt + 2;            
            }

            GameObject meshGo = new GameObject("Round");
            meshGo.transform.parent = _parent.transform;
            meshGo.transform.localPosition = _localPosition;

            MeshRenderer render = meshGo.AddComponent<MeshRenderer>();
            render.sortingOrder = _order;
            render.material = new Material(Shader.Find("Sprites/Default"));
            MeshFilter meshFilter = meshGo.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.vertices = vtxPos;
            mesh.triangles = triangles;
            mesh.colors = colors;
            meshFilter.mesh = mesh;

            return meshFilter;
        }

        /// <summary>
        /// テキスト
        /// </summary>
        public static TextMesh RenderText(GameObject _parent, string _text , float _size, Vector3 _localPosition, Color _color , int _order = 0 , FontStyle _fontStyle = FontStyle.Normal)
        {
            GameObject textGo = new GameObject("Text");
            textGo.transform.parent = _parent.transform;
            textGo.transform.localPosition = _localPosition;
            TextMesh textMesh = textGo.AddComponent<TextMesh>();
            textMesh.text = _text;
            textMesh.fontSize = 100;
            textMesh.characterSize = _size;
            textMesh.color = _color;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.fontStyle = _fontStyle;
            MeshRenderer render = textGo.GetComponent<MeshRenderer>(); 
            render.sortingOrder = _order;        
            return textMesh;
        }
    }
}