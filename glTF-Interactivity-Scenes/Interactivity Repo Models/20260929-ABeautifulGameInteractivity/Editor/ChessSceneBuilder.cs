using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Chess.Editor
{
    /// <summary>
    /// Editor tool that builds a ready-to-export chess scene from the Khronos "ABeautifulGame"
    /// sample: instantiates the model, tags every piece with <see cref="ChessPiece"/> (color/type
    /// from its node name), puts all pieces on the standard starting squares (the model ships in a
    /// mid-game position: 1.d4 e6 2.c3), generates the 64 clickable board squares
    /// (<see cref="ChessSquare"/> + a thin quad so glTF viewers have geometry to raycast against),
    /// and adds <see cref="ChessInteractivityExport"/> to the board root.
    ///
    /// The 8x8 grid is inferred from the model instead of being hard-coded: square size from the
    /// spacing between pieces, orientation from where the white/black armies stand, and the grid
    /// center from the "Chessboard" mesh bounds. Everything is computed in the model root's local
    /// space so it doesn't matter where the model is placed.
    /// </summary>
    public static class ChessSceneBuilder
    {
        const string GlbPath = "Packages/com.needle.gltf-interactivity-scenes/Test Scenes/20250212-ABeautifulGame/ABeautifulGame.glb";
        const string SampleFolder = "Packages/com.needle.gltf-interactivity-scenes/Interactivity Repo Models/20260929-ABeautifulGameInteractivity";
        const string ScenePath = SampleFolder + "/ABeautifulGameInteractivity.unity";
        const string SquareMaterialPath = SampleFolder + "/Materials/ChessSquare.mat";
        const string BoardMeshNodeName = "Chessboard";

        // King_W, Queen_B, Castle_W1, Knight_B2, Bishop_W1, Pawn_Body_W3 ... (Pawn_Top_* are
        // child meshes of Pawn_Body_* and deliberately don't match).
        static readonly Regex PieceNamePattern = new Regex(@"^(King|Queen|Castle|Rook|Knight|Bishop|Pawn_Body|Pawn)_([WB])\d*$");

        static readonly ChessPieceType[] BackRank =
        {
            ChessPieceType.Rook, ChessPieceType.Knight, ChessPieceType.Bishop, ChessPieceType.Queen,
            ChessPieceType.King, ChessPieceType.Bishop, ChessPieceType.Knight, ChessPieceType.Rook,
        };

        struct Grid
        {
            public Vector3 center;   // local space of the model root, y = square surface height
            public Vector3 fileAxis; // a -> h, one unit vector
            public Vector3 rankAxis; // 1 -> 8 (white -> black), one unit vector
            public float squareSize;

            public Vector3 SquareCenter(int file, int rank) =>
                center + fileAxis * ((file - 3.5f) * squareSize) + rankAxis * ((rank - 3.5f) * squareSize);

            public Vector2Int SquareOf(Vector3 localPos)
            {
                var d = localPos - center;
                var f = Mathf.RoundToInt(Vector3.Dot(d, fileAxis) / squareSize + 3.5f);
                var r = Mathf.RoundToInt(Vector3.Dot(d, rankAxis) / squareSize + 3.5f);
                return new Vector2Int(f, r);
            }
        }

        [MenuItem("Chess/Build Chess Interactivity Scene")]
        public static void Build()
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (modelAsset == null)
            {
                Debug.LogError($"ChessSceneBuilder: could not load '{GlbPath}'. Is the com.needle.gltf-interactivity-scenes package installed?");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var model = (GameObject) PrefabUtility.InstantiatePrefab(modelAsset, scene);
            // Plain scene objects instead of a GLB prefab instance: everything below (piece tags,
            // moved pieces, squares, the embedded script graph) would otherwise be prefab overrides,
            // and Visual Scripting doesn't support embed graphs on prefab instances well.
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "ChessBoard";
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            var root = model.transform;

            var pieces = CollectPieces(root);
            if (pieces.Count != 32)
            {
                Debug.LogError($"ChessSceneBuilder: expected 32 chess pieces under the model root, found {pieces.Count}. " +
                               "The model's node names don't match ABeautifulGame - aborting.");
                return;
            }

            var grid = InferGrid(root, pieces);
            PlaceAtStartingPosition(root, pieces, grid);

            BuildSquares(root, grid);

            var chessExport = model.AddComponent<ChessInteractivityExport>();
            ConfigureCaptureZone(chessExport, root, pieces, grid);

            // The visual scripting exporter only runs (and with it the IInteractivityExport
            // callbacks) when the export contains an active ScriptMachine with a graph.
            // The chess logic lives in ChessInteractivityExport, so an empty embedded graph is enough.
            var scriptMachine = model.AddComponent<ScriptMachine>();
            scriptMachine.nest.SwitchToEmbed(new FlowGraph());

            FrameCamera(root, grid);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"ChessSceneBuilder: scene saved to {ScenePath} (square size {grid.squareSize:F4}). " +
                      "Export the glTF/glb from this scene to get a playable chess set with rules baked into KHR_interactivity.");
        }

        // ------------------------------------------------------------------------------------
        // Pieces
        // ------------------------------------------------------------------------------------

        static List<ChessPiece> CollectPieces(Transform root)
        {
            var result = new List<ChessPiece>();
            // Only direct children: ChessInteractivityExport moves pieces by writing their local
            // translation, which assumes they all share the model root as parent.
            foreach (Transform t in root)
            {
                var match = PieceNamePattern.Match(t.name);
                if (!match.Success) continue;

                var piece = t.GetComponent<ChessPiece>();
                if (piece == null) piece = t.gameObject.AddComponent<ChessPiece>();
                piece.type = TypeFromName(match.Groups[1].Value);
                piece.color = match.Groups[2].Value == "W" ? ChessColor.White : ChessColor.Black;
                result.Add(piece);
            }
            return result;
        }

        static ChessPieceType TypeFromName(string n)
        {
            switch (n)
            {
                case "King": return ChessPieceType.King;
                case "Queen": return ChessPieceType.Queen;
                case "Castle":
                case "Rook": return ChessPieceType.Rook;
                case "Knight": return ChessPieceType.Knight;
                case "Bishop": return ChessPieceType.Bishop;
                default: return ChessPieceType.Pawn;
            }
        }

        /// <summary>
        /// Assigns every piece its standard starting square and moves it there (keeping its own
        /// height, so pieces keep sitting on the board exactly like in the source model). Pieces
        /// of the same color/type are matched to their start squares in file order, so pieces
        /// that are already on their start square stay put.
        /// </summary>
        static void PlaceAtStartingPosition(Transform root, List<ChessPiece> pieces, Grid grid)
        {
            foreach (var group in pieces.GroupBy(p => (p.color, p.type)))
            {
                var (color, type) = group.Key;
                var backRank = color == ChessColor.White ? 0 : 7;
                var pawnRank = color == ChessColor.White ? 1 : 6;

                var startSquares = type == ChessPieceType.Pawn
                    ? Enumerable.Range(0, 8).Select(f => new Vector2Int(f, pawnRank)).ToList()
                    : Enumerable.Range(0, 8).Where(f => BackRank[f] == type).Select(f => new Vector2Int(f, backRank)).ToList();

                var ordered = group.OrderBy(p => grid.SquareOf(p.transform.localPosition).x).ToList();
                if (ordered.Count != startSquares.Count)
                    Debug.LogWarning($"ChessSceneBuilder: found {ordered.Count} {color} {type}(s), expected {startSquares.Count}.");

                for (var i = 0; i < ordered.Count && i < startSquares.Count; i++)
                {
                    var piece = ordered[i];
                    var sq = startSquares[i];
                    piece.startFile = sq.x;
                    piece.startRank = sq.y;

                    var target = grid.SquareCenter(sq.x, sq.y);
                    var t = piece.transform;
                    t.localPosition = new Vector3(target.x, t.localPosition.y, target.z);
                }
            }
        }

        // ------------------------------------------------------------------------------------
        // Grid inference
        // ------------------------------------------------------------------------------------

        static Grid InferGrid(Transform root, List<ChessPiece> pieces)
        {
            var local = pieces.Select(p => p.transform.localPosition).ToArray();

            // Square size: most pieces stand next to another one (pawn rows, back rank), so the
            // median nearest-neighbour distance is the square size.
            var nearest = new List<float>();
            for (var i = 0; i < local.Length; i++)
            {
                var best = float.MaxValue;
                for (var j = 0; j < local.Length; j++)
                {
                    if (i == j) continue;
                    var d = Mathf.Max(Mathf.Abs(local[i].x - local[j].x), Mathf.Abs(local[i].z - local[j].z));
                    if (d > 1e-4f && d < best) best = d;
                }
                if (best < float.MaxValue) nearest.Add(best);
            }
            nearest.Sort();
            var squareSize = nearest.Count > 0 ? nearest[nearest.Count / 2] : 0.0625f;

            // Ranks run from the white army towards the black army; snap to the closest local axis.
            var whiteCenter = Average(pieces.Where(p => p.color == ChessColor.White).Select(p => p.transform.localPosition));
            var blackCenter = Average(pieces.Where(p => p.color == ChessColor.Black).Select(p => p.transform.localPosition));
            var towardsBlack = blackCenter - whiteCenter;
            var rankAxis = Mathf.Abs(towardsBlack.x) > Mathf.Abs(towardsBlack.z)
                ? new Vector3(Mathf.Sign(towardsBlack.x), 0, 0)
                : new Vector3(0, 0, Mathf.Sign(towardsBlack.z));
            // White looks along +rank; files a..h go from white's left to right (Unity is left-handed).
            var fileAxis = Vector3.Cross(Vector3.up, rankAxis);

            // Grid center: the board mesh is symmetric around the playing area. Fall back to the
            // white king (always e1 in the source model) if there is no board mesh.
            Vector3 center;
            var boardRenderer = FindBoardRenderer(root);
            if (boardRenderer != null)
            {
                center = root.InverseTransformPoint(boardRenderer.bounds.center);
            }
            else
            {
                var whiteKing = pieces.First(p => p.color == ChessColor.White && p.type == ChessPieceType.King).transform.localPosition;
                center = whiteKing + fileAxis * (-0.5f * squareSize) + rankAxis * (3.5f * squareSize);
            }

            var grid = new Grid {center = center, fileAxis = fileAxis, rankAxis = rankAxis, squareSize = squareSize};

            // The nearest-neighbour estimate is skewed by pieces not standing exactly centered;
            // refine it with a least-squares fit of all pieces against their snapped squares.
            float num = 0, den = 0;
            foreach (var p in local)
            {
                var sq = grid.SquareOf(p);
                var d = p - center;
                float kf = sq.x - 3.5f, kr = sq.y - 3.5f;
                num += Vector3.Dot(d, fileAxis) * kf + Vector3.Dot(d, rankAxis) * kr;
                den += kf * kf + kr * kr;
            }
            if (den > 0 && num > 0) grid.squareSize = num / den;

            // Clickable squares float just above where the pieces stand.
            grid.center.y = local.Max(p => p.y) + grid.squareSize * 0.02f;

            return grid;
        }

        static Vector3 Average(IEnumerable<Vector3> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? Vector3.zero : list.Aggregate(Vector3.zero, (a, b) => a + b) / list.Count;
        }

        // ------------------------------------------------------------------------------------
        // Squares
        // ------------------------------------------------------------------------------------

        static void BuildSquares(Transform boardRoot, Grid grid)
        {
            var materialTemplate = GetOrCreateSquareMaterial();

            var squaresRoot = new GameObject("Squares").transform;
            squaresRoot.SetParent(boardRoot, false);

            // Quad faces -Z by default; lay it flat facing up, aligned with the file/rank axes.
            var squareRotation = Quaternion.LookRotation(Vector3.down, grid.rankAxis);

            for (var r = 0; r < 8; r++)
            for (var f = 0; f < 8; f++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Square_{(char) ('a' + f)}{r + 1}";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(squaresRoot, false);
                quad.transform.localPosition = grid.SquareCenter(f, r);
                quad.transform.localRotation = squareRotation;
                quad.transform.localScale = new Vector3(grid.squareSize, grid.squareSize, 1f);

                // Each square needs its own material instance (not a shared one) so the
                // interactivity graph can tint individual squares (legal-move highlight)
                // without affecting the others.
                var material = new Material(materialTemplate) {name = $"ChessSquare_{quad.name}"};
                var meshRenderer = quad.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                var square = quad.AddComponent<ChessSquare>();
                square.file = f;
                square.rank = r;
            }
        }

        // ------------------------------------------------------------------------------------
        // Capture zone
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Captured pieces line up next to the board (beside the a- and h-file), standing at the
        /// height of the board's underside - as if on the table the board lies on.
        /// </summary>
        static void ConfigureCaptureZone(ChessInteractivityExport chessExport, Transform root, List<ChessPiece> pieces, Grid grid)
        {
            // Beyond the board edge, plus room for the piece's footprint.
            const float clearanceInSquares = 0.8f;
            var edgeDistanceInSquares = 1f;
            var boardRenderer = FindBoardRenderer(root);
            var pieceHeight = pieces.Min(p => p.transform.localPosition.y);
            var tableHeight = pieceHeight;
            if (boardRenderer != null)
            {
                var min = root.InverseTransformPoint(boardRenderer.bounds.min);
                var max = root.InverseTransformPoint(boardRenderer.bounds.max);
                var boardHalfAlongFiles = Mathf.Abs(Vector3.Dot((max - min) * 0.5f, grid.fileAxis));
                edgeDistanceInSquares = boardHalfAlongFiles / grid.squareSize - 3.5f;
                tableHeight = Mathf.Min(min.y, max.y);
            }

            chessExport.captureZoneDistance = edgeDistanceInSquares + clearanceInSquares;
            chessExport.captureZoneHeightOffset = tableHeight - pieceHeight;
        }

        static Material GetOrCreateSquareMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SquareMaterialPath);
            if (existing != null) return existing;

            EnsureFolder(SampleFolder + "/Materials");
            var shader = Shader.Find("Standard");
            var mat = new Material(shader) {name = "ChessSquare"};
            mat.SetFloat("_Mode", 3f); // Transparent
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mat.color = new Color(1f, 1f, 1f, 0.12f);

            AssetDatabase.CreateAsset(mat, SquareMaterialPath);
            return mat;
        }

        // ------------------------------------------------------------------------------------
        // Scene helpers
        // ------------------------------------------------------------------------------------

        /// <summary>The default scene camera sits 10m away - frame the (70cm) board from white's side instead.</summary>
        static void FrameCamera(Transform root, Grid grid)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var boardSpan = grid.squareSize * 8f;
            var target = root.TransformPoint(grid.center);
            var back = root.TransformDirection(-grid.rankAxis);
            cam.transform.position = target + back * (boardSpan * 1.3f) + root.up * (boardSpan * 1.2f);
            cam.transform.LookAt(target, root.up);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
        }

        static Renderer FindBoardRenderer(Transform root)
        {
            var boardNode = root.Find(BoardMeshNodeName);
            return boardNode ? boardNode.GetComponent<Renderer>() : null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
