using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Chess
{
    /// <summary>
    /// Injects a KHR_interactivity graph that turns the pieces and squares found under this
    /// GameObject (via <see cref="ChessPiece"/> / <see cref="ChessSquare"/> components) into a
    /// playable, rules-enforcing chess set: click a piece, then click a square (or an opponent's
    /// piece) to move it there if (and only if) that move is legal for the piece's type; moving
    /// onto an opponent's piece captures it (makes it unselectable and moves it off the board into
    /// that color's capture zone). Clicking the selected piece again deselects it. Capturing a king
    /// ends the game (no more moves). Bishops, rooks and queens can't jump.
    ///
    /// The squares under the pieces of the player to move are tinted gold (while a piece is selected
    /// only its own square, plus its legal moves in green); at game end the winner's squares switch
    /// to <see cref="WinnerSquareColor"/>. Moves are animated as a short hop via
    /// `pointer/interpolate`; on a capture the captured piece then hops to the next free slot beside
    /// the board. Piece clicks are ignored while anything is in the air.
    ///
    /// Graph structure - everything exists once, not per piece / per square:
    /// - Board state lives in fixed-capacity, variable-backed arrays (<see cref="VariableBasedList"/>,
    ///   read and written via <see cref="ListHelpers"/>): the piece on each square, the square of each
    ///   piece, and the number of free squares in each direction from the selected piece.
    /// - Per-piece / per-square constants (glTF node, type, color, material, ...) are `math/switch`
    ///   lookups with literal cases.
    /// - Clicks: each piece/square has an `event/onSelect` that only stores its index and jumps into
    ///   one shared handler.
    /// - Legality is one expression over "the square in <c>chess_evalSquare</c>": evaluated in a
    ///   `flow/for` over all 64 squares to recolor the board, and for the clicked square on a move.
    /// - Animations write `/nodes/{nodeIndex}/translation` with the node index taken from a variable.
    ///
    /// Custom events for the host application (see <see cref="EventTurn"/> etc.): turn changes,
    /// selection, moves, captures and game over, with the piece (index + glTF node) and squares.
    ///
    /// Expects all pieces to be direct children of one common parent (pieces are moved by writing
    /// their local translation) and the squares' centers to lie in that parent's local XZ plane.
    ///
    /// Scope / known limitations:
    /// - No check / checkmate / stalemate detection - illegal-because-it-leaves-your-king-in-check
    ///   moves are not rejected.
    /// - No castling, en passant, or pawn promotion.
    /// - No text UI (use the custom events) and no AI/autoplay - two-player, click-driven only.
    /// </summary>
    public class ChessInteractivityExport : MonoBehaviour, IInteractivityExport
    {
        // Custom events sent to the host application. Colors: 0 = white, 1 = black. Pieces: index
        // into this component's ChessPiece list (stable per export) plus the glTF node index.
        // Squares: file 0-7 = a-h, rank 0-7 = 1-8.
        public const string EventTurn = "chess/turn";                // color - sent on start and after every move
        public const string EventSelected = "chess/selected";        // piece, node, pieceType, color, file, rank
        public const string EventDeselected = "chess/deselected";    // piece, node
        public const string EventMoved = "chess/moved";              // piece, node, pieceType, color, fromFile, fromRank, toFile, toRank, capturedPiece (-1 = none)
        public const string EventCaptured = "chess/captured";        // piece, node, pieceType, color (of the captured piece)
        public const string EventGameOver = "chess/gameOver";        // winner

        const float HighlightScaleFactor = 1.2f;
        static readonly Color LegalMoveSquareColor = new Color(0.25f, 0.9f, 0.25f, 0.55f);
        static readonly Color MovablePieceSquareColor = new Color(1f, 0.72f, 0.1f, 0.5f);
        static readonly Color WinnerSquareColor = new Color(0.25f, 0.55f, 1f, 0.6f);

        const float MoveDuration = 0.4f;
        /// <summary>The hop arc is sampled into this many linear `pointer/interpolate` segments.</summary>
        const int MoveSegments = 6;
        /// <summary>Hop height relative to the square size.</summary>
        const float MoveHopHeight = 0.6f;
        const float CaptureDuration = 0.5f;
        const float CaptureHopHeight = 1.2f;
        const int CaptureSlotsPerRow = 8;
        const int MaxCapturedPerColor = 16;

        [Header("Captured pieces")]
        [Tooltip("Distance of the first row of captured pieces from the center of the outermost file (a or h), in squares.")]
        public float captureZoneDistance = 2.5f;
        [Tooltip("Height offset (pieces' parent space) of captured pieces relative to their height on the board, e.g. to stand next to the board instead of floating at board height.")]
        public float captureZoneHeightOffset = 0f;

        // --- export-time data -----------------------------------------------------------------
        GltfInteractivityExportNodes export;
        ChessPiece[] pieces;
        readonly ChessSquare[] squares = new ChessSquare[64]; // index = rank * 8 + file
        int[] pieceNodes;
        int[] squareMaterials;
        // Square grid in the pieces' parent space: center(f, r) = gridOrigin + f * gridFileStep + r * gridRankStep
        Vector3 gridOrigin, gridFileStep, gridRankStep;
        float squareSize;

        // --- variables ------------------------------------------------------------------------
        int turnVar, hasSelectionVar, gameOverVar, animatingVar;
        int selPieceVar, selSquareVar, selFileVar, selRankVar, selTypeVar, selColorVar;
        int clickedPieceVar, clickedSquareVar, evalSquareVar, walkFreeVar, walkBlockedVar;
        int mvPieceVar, mvFromVar, mvToVar, mvCapturedVar;
        int mvStartXVar, mvStartZVar, mvTargetXVar, mvTargetZVar, capTargetXVar, capTargetZVar;
        int animMoverNodeVar, animMoverYVar, animCapturedNodeVar, animCapturedYVar;
        readonly int[] capturedCountVar = new int[2];

        /// <summary>Piece index on each square (-1 = empty).</summary>
        VariableBasedList squarePieceList;
        /// <summary>Square index of each piece (-1 = captured).</summary>
        VariableBasedList pieceSquareList;
        /// <summary>Free squares from the selected piece per direction, index (sign(df)+1)*3 + sign(dr)+1.</summary>
        VariableBasedList freeStepsList;

        int turnEvent, selectedEvent, deselectedEvent, movedEvent, capturedEvent, gameOverEvent;

        public void OnInteractivityExport(GltfInteractivityExportNodes export)
        {
            this.export = export;
            var sceneExporter = export.Context.exporter;

            pieces = GetComponentsInChildren<ChessPiece>(true);
            System.Array.Clear(squares, 0, squares.Length);
            foreach (var square in GetComponentsInChildren<ChessSquare>(true))
                squares[square.rank * 8 + square.file] = square;

            if (pieces.Length == 0 || squares.Any(s => s == null))
            {
                Debug.LogWarning($"ChessInteractivityExport on '{name}': needs ChessPiece components and all 64 ChessSquares, skipping graph generation.", this);
                return;
            }
            var pieceParent = pieces[0].transform.parent;
            if (pieces.Any(p => p.transform.parent != pieceParent))
            {
                Debug.LogError($"ChessInteractivityExport on '{name}': all ChessPiece objects must share the same parent, skipping graph generation.", this);
                return;
            }
            if (pieces.Any(p => squares.Count(s => s.file == p.startFile && s.rank == p.startRank) != 1)
                || pieces.Select(p => p.startRank * 8 + p.startFile).Distinct().Count() != pieces.Length)
            {
                Debug.LogError($"ChessInteractivityExport on '{name}': pieces need distinct start squares, skipping graph generation.", this);
                return;
            }

            ComputeGrid(pieceParent);
            pieceNodes = pieces.Select(p => sceneExporter.GetTransformIndex(p.transform)).ToArray();
            squareMaterials = squares.Select(s => sceneExporter.GetMaterialIndex(s.GetComponent<MeshRenderer>().sharedMaterial)).ToArray();

            DeclareVariablesAndLists();
            DeclareEvents();

            var legal = BuildLegality();
            var refreshEntry = BuildBoardRefresh(legal);
            var deselectEntry = BuildDeselect(refreshEntry);
            var selectEntry = BuildSelect(refreshEntry);
            var moveEntry = BuildMove(refreshEntry);
            var squareClickedEntry = BuildSquareClicked(legal, moveEntry);
            var pieceClickedEntry = BuildPieceClicked(selectEntry, deselectEntry, squareClickedEntry);

            for (var i = 0; i < pieces.Length; i++)
            {
                export.Context.AddSelectabilityExtensionToNode(pieceNodes[i], true);
                OnSelectStoresIndex(pieceNodes[i], clickedPieceVar, i, pieceClickedEntry);
            }
            for (var s = 0; s < 64; s++)
            {
                var node = sceneExporter.GetTransformIndex(squares[s].transform);
                export.Context.AddSelectabilityExtensionToNode(node, true);
                OnSelectStoresIndex(node, clickedSquareVar, s, squareClickedEntry);
            }

            // Tell the host who starts and paint the initial state.
            var onStart = export.CreateNode<Event_OnStartNode>();
            var sendTurn = Send(turnEvent, ("color", Get(turnVar)));
            onStart.FlowOut(Event_OnStartNode.IdFlowOut).ConnectToFlowDestination(sendTurn.FlowIn(Event_SendNode.IdFlowIn));
            sendTurn.FlowOut(Event_SendNode.IdFlowOut).ConnectToFlowDestination(refreshEntry);
        }

        /// <summary>Derives the (linear) square grid in the pieces' parent space from squares a1, b1 and a2.</summary>
        void ComputeGrid(Transform pieceParent)
        {
            Vector3 LocalCenter(int index) => pieceParent.InverseTransformPoint(squares[index].transform.position);
            gridOrigin = LocalCenter(0);
            gridFileStep = LocalCenter(1) - gridOrigin;
            gridRankStep = LocalCenter(8) - gridOrigin;
            squareSize = gridFileStep.magnitude;
        }

        // ------------------------------------------------------------------------------------
        // Variables, arrays and events
        // ------------------------------------------------------------------------------------

        void DeclareVariablesAndLists()
        {
            var ctx = export.Context;
            int Int(string id, int value = -1) => ctx.AddVariableWithIdIfNeeded("chess_" + id, value, GltfTypes.Int);
            int Bool(string id) => ctx.AddVariableWithIdIfNeeded("chess_" + id, false, GltfTypes.Bool);
            int Float(string id) => ctx.AddVariableWithIdIfNeeded("chess_" + id, 0f, GltfTypes.Float);

            turnVar = Int("turn", (int) ChessColor.White);
            hasSelectionVar = Bool("hasSelection");
            gameOverVar = Bool("gameOver");
            // True while a move (incl. the captured piece's hop) is animated. The animations read
            // the mv*/cap*/anim* variables while running, so no new move may begin before that.
            animatingVar = Bool("animating");

            selPieceVar = Int("selPiece");
            selSquareVar = Int("selSquare");
            selFileVar = Int("selFile");
            selRankVar = Int("selRank");
            selTypeVar = Int("selType");
            selColorVar = Int("selColor");

            clickedPieceVar = Int("clickedPiece");
            clickedSquareVar = Int("clickedSquare");
            // The square the legality expression is evaluated for.
            evalSquareVar = Int("evalSquare", 0);
            walkFreeVar = Int("walkFree", 0);
            walkBlockedVar = Bool("walkBlocked");

            // Snapshot of the current move (read by the animations until everything has landed).
            mvPieceVar = Int("mvPiece");
            mvFromVar = Int("mvFrom");
            mvToVar = Int("mvTo");
            mvCapturedVar = Int("mvCaptured");
            mvStartXVar = Float("mvStartX");
            mvStartZVar = Float("mvStartZ");
            mvTargetXVar = Float("mvTargetX");
            mvTargetZVar = Float("mvTargetZ");
            capTargetXVar = Float("capTargetX");
            capTargetZVar = Float("capTargetZ");
            animMoverNodeVar = Int("animMoverNode", 0);
            animMoverYVar = Float("animMoverY");
            animCapturedNodeVar = Int("animCapturedNode", 0);
            animCapturedYVar = Float("animCapturedY");
            capturedCountVar[(int) ChessColor.White] = Int("capturedCountWhite", 0);
            capturedCountVar[(int) ChessColor.Black] = Int("capturedCountBlack", 0);

            var intType = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Int);
            squarePieceList = new VariableBasedList(ctx, "chess_squarePiece", 64, intType);
            for (var s = 0; s < 64; s++)
                squarePieceList.AddItem(System.Array.FindIndex(pieces, p => p.startRank * 8 + p.startFile == s));

            pieceSquareList = new VariableBasedList(ctx, "chess_pieceSquare", pieces.Length, intType);
            foreach (var piece in pieces)
                pieceSquareList.AddItem(piece.startRank * 8 + piece.startFile);

            freeStepsList = new VariableBasedList(ctx, "chess_freeSteps", 9, intType);
            for (var d = 0; d < 9; d++)
                freeStepsList.AddItem(0);
        }

        void DeclareEvents()
        {
            int Declare(string id, params string[] intValues)
            {
                var values = intValues.ToDictionary(v => v, _ => new GltfInteractivityNode.EventValues
                {
                    Type = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Int),
                    Value = -1,
                });
                return export.Context.AddEventWithIdIfNeeded(id, values);
            }

            turnEvent = Declare(EventTurn, "color");
            selectedEvent = Declare(EventSelected, "piece", "node", "pieceType", "color", "file", "rank");
            deselectedEvent = Declare(EventDeselected, "piece", "node");
            movedEvent = Declare(EventMoved, "piece", "node", "pieceType", "color", "fromFile", "fromRank", "toFile", "toRank", "capturedPiece");
            capturedEvent = Declare(EventCaptured, "piece", "node", "pieceType", "color");
            gameOverEvent = Declare(EventGameOver, "winner");
        }

        GltfInteractivityExportNode Send(int eventId, params (string name, object value)[] values)
        {
            var node = export.CreateNode<Event_SendNode>();
            node.Configuration[Event_SendNode.IdEvent].Value = eventId;
            foreach (var (valueName, value) in values)
                Bind(node.ValueIn(valueName), value);
            return node;
        }

        // ------------------------------------------------------------------------------------
        // Lookups of per-piece / per-square constants
        // ------------------------------------------------------------------------------------

        /// <summary>`math/switch` over index 0..n-1 with literal cases.</summary>
        ValueOutRef Lookup<T>(object index, IReadOnlyList<T> values, T defaultValue)
        {
            var node = export.CreateNode<Math_SwitchNode>();
            node.Configuration[Math_SwitchNode.IdConfigCases].Value = Enumerable.Range(0, values.Count).ToArray();
            Bind(node.ValueIn(Math_SwitchNode.IdSelection), index);
            node.ValueIn(Math_SwitchNode.IdSelection).SetType(TypeRestriction.LimitToInt);
            node.ValueIn(Math_SwitchNode.IdDefaultValue).SetValue(defaultValue);
            for (var i = 0; i < values.Count; i++)
                node.ValueIn(i.ToString()).SetValue(values[i]);
            return node.ValueOut(Math_SwitchNode.IdOut);
        }

        ValueOutRef PieceNode(object pieceIndex) => Lookup(pieceIndex, pieceNodes, -1);
        ValueOutRef PieceType(object pieceIndex) => Lookup(pieceIndex, pieces.Select(p => (int) p.type).ToArray(), -1);
        ValueOutRef PieceColor(object pieceIndex) => Lookup(pieceIndex, pieces.Select(p => (int) p.color).ToArray(), -1);
        ValueOutRef PieceHeight(object pieceIndex) => Lookup(pieceIndex, pieces.Select(p => p.transform.localPosition.y).ToArray(), 0f);
        ValueOutRef PieceScale(object pieceIndex) => Lookup(pieceIndex, pieces.Select(p => p.transform.localScale).ToArray(), Vector3.one);

        ValueOutRef GetItem(VariableBasedList list, object index)
        {
            ListHelpers.GetItem(export, list, out var indexInput, out var value);
            Bind(indexInput, index);
            return value;
        }

        /// <summary>list[index] = value; returns the entry, continues at <paramref name="flowOut"/>.</summary>
        FlowInRef SetItem(VariableBasedList list, object index, object value, out FlowOutRef flowOut)
        {
            ListHelpers.SetItem(export, list, out var indexInput, out var valueInput, out var flowIn, out flowOut);
            Bind(indexInput, index);
            Bind(valueInput, value);
            return flowIn;
        }

        ValueOutRef File(object square) => MathBin<Math_RemNode>(square, 8);
        ValueOutRef Rank(object square) => MathBin<Math_DivNode>(square, 8);

        /// <summary>x or z (by <paramref name="axis"/>) of a square center in the pieces' parent space.</summary>
        ValueOutRef GridCoordinate(object file, object rank, int axis)
        {
            return MathBin<Math_AddNode>(gridOrigin[axis],
                MathBin<Math_AddNode>(MathBin<Math_MulNode>(IntToFloat(file), gridFileStep[axis]),
                    MathBin<Math_MulNode>(IntToFloat(rank), gridRankStep[axis])));
        }

        // ------------------------------------------------------------------------------------
        // Clicks
        // ------------------------------------------------------------------------------------

        void OnSelectStoresIndex(int nodeIndex, int variable, int index, FlowInRef handler)
        {
            var onSelect = export.CreateNode<Event_OnSelectNode>();
            onSelect.Configuration[Event_OnSelectNode.IdConfigNodeIndex].Value = nodeIndex;
            var set = SetVars(out var setOut, (variable, index));
            onSelect.FlowOut(Event_OnSelectNode.IdFlowOut).ConnectToFlowDestination(set);
            setOut.ConnectToFlowDestination(handler);
        }

        /// <summary>
        /// Clicked piece (<see cref="clickedPieceVar"/>):
        /// own piece -> select it, or deselect it if it is the selected one;
        /// opponent's piece -> treat as a click on its square (captures work by clicking the piece).
        /// Ignored for captured pieces, after the game ended and while something is animated.
        /// </summary>
        FlowInRef BuildPieceClicked(FlowInRef selectEntry, FlowInRef deselectEntry, FlowInRef squareClickedEntry)
        {
            var clicked = Get(clickedPieceVar);
            var square = GetItem(pieceSquareList, clicked);

            var active = Branch(AndAll(MathBin<Math_GeNode>(square, 0), MathUn<Math_NotNode>(Get(gameOverVar)), MathUn<Math_NotNode>(Get(animatingVar))));
            var ownPiece = Branch(MathBin<Math_EqNode>(PieceColor(clicked), Get(turnVar)));
            active.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(ownPiece.FlowIn(Flow_BranchNode.IdFlowIn));

            var isSelected = Branch(MathBin<Math_AndNode>(Get(hasSelectionVar), MathBin<Math_EqNode>(Get(selPieceVar), clicked)));
            ownPiece.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(isSelected.FlowIn(Flow_BranchNode.IdFlowIn));
            isSelected.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(deselectEntry);
            isSelected.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(selectEntry);

            var forward = SetVars(out var forwardOut, (clickedSquareVar, square));
            ownPiece.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(forward);
            forwardOut.ConnectToFlowDestination(squareClickedEntry);

            return active.FlowIn(Flow_BranchNode.IdFlowIn);
        }

        /// <summary>Clicked square (<see cref="clickedSquareVar"/>): move there if a piece is selected and the move is legal.</summary>
        FlowInRef BuildSquareClicked(ValueOutRef legal, FlowInRef moveEntry)
        {
            var hasSelection = Branch(Get(hasSelectionVar));
            var evalClicked = SetVars(out var evalOut, (evalSquareVar, Get(clickedSquareVar)));
            var isLegal = Branch(legal);
            hasSelection.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(evalClicked);
            evalOut.ConnectToFlowDestination(isLegal.FlowIn(Flow_BranchNode.IdFlowIn));
            isLegal.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(moveEntry);
            return hasSelection.FlowIn(Flow_BranchNode.IdFlowIn);
        }

        // ------------------------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------------------------

        /// <summary>`pointer/set` of a piece's scale (piece index from a value) to its original scale * factor.</summary>
        FlowInRef SetPieceScale(object pieceIndex, float factor, out FlowOutRef flowOut)
        {
            var node = export.CreateNode<Pointer_SetNode>();
            PointersHelper.AddPointerConfig(node, PointersHelper.IdPointerTemplNodeByIndex + "/scale", GltfTypes.Float3);
            PointersHelper.AddPointerTemplateValueInput(node, PointersHelper.IdPointerNodeIndex);
            Bind(node.ValueIn(PointersHelper.IdPointerNodeIndex), PieceNode(pieceIndex));
            var scale = PieceScale(pieceIndex);
            Bind(node.ValueIn(Pointer_SetNode.IdValue), Mathf.Approximately(factor, 1f) ? scale : MathBin<Math_MulNode>(scale, Vector3.one * factor));
            flowOut = node.FlowOut(Pointer_SetNode.IdFlowOut);
            return node.FlowIn(Pointer_SetNode.IdFlowIn);
        }

        /// <summary>Selects <see cref="clickedPieceVar"/> (un-highlighting a previous selection) and refreshes the board.</summary>
        FlowInRef BuildSelect(FlowInRef refreshEntry)
        {
            var hadSelection = Branch(Get(hasSelectionVar));
            var unhighlight = SetPieceScale(Get(selPieceVar), 1f, out var unhighlightOut);
            hadSelection.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(unhighlight);

            var clicked = Get(clickedPieceVar);
            var square = GetItem(pieceSquareList, clicked);
            var apply = SetVars(out var applyOut,
                (selPieceVar, clicked), (selSquareVar, square), (selFileVar, File(square)), (selRankVar, Rank(square)),
                (selTypeVar, PieceType(clicked)), (selColorVar, PieceColor(clicked)), (hasSelectionVar, true));
            hadSelection.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(apply);
            unhighlightOut.ConnectToFlowDestination(apply);

            var highlight = SetPieceScale(Get(selPieceVar), HighlightScaleFactor, out var highlightOut);
            applyOut.ConnectToFlowDestination(highlight);

            var send = Send(selectedEvent, ("piece", Get(selPieceVar)), ("node", PieceNode(Get(selPieceVar))), ("pieceType", Get(selTypeVar)),
                ("color", Get(selColorVar)), ("file", Get(selFileVar)), ("rank", Get(selRankVar)));
            highlightOut.ConnectToFlowDestination(send.FlowIn(Event_SendNode.IdFlowIn));
            send.FlowOut(Event_SendNode.IdFlowOut).ConnectToFlowDestination(refreshEntry);

            return hadSelection.FlowIn(Flow_BranchNode.IdFlowIn);
        }

        FlowInRef BuildDeselect(FlowInRef refreshEntry)
        {
            var unhighlight = SetPieceScale(Get(selPieceVar), 1f, out var unhighlightOut);
            var send = Send(deselectedEvent, ("piece", Get(selPieceVar)), ("node", PieceNode(Get(selPieceVar))));
            unhighlightOut.ConnectToFlowDestination(send.FlowIn(Event_SendNode.IdFlowIn));
            var clear = SetVars(out var clearOut, (hasSelectionVar, false), (selPieceVar, -1));
            send.FlowOut(Event_SendNode.IdFlowOut).ConnectToFlowDestination(clear);
            clearOut.ConnectToFlowDestination(refreshEntry);
            return unhighlight;
        }

        // ------------------------------------------------------------------------------------
        // Legality
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// True if the selected piece may move to the square in <see cref="evalSquareVar"/>: the
        /// movement shape of its type allows it, the target isn't occupied by an own piece, pawns
        /// only capture diagonally, and bishops/rooks/queens don't jump (every square in between is
        /// free - see <see cref="BuildFreeStepsWalk"/>).
        /// </summary>
        ValueOutRef BuildLegality()
        {
            var target = Get(evalSquareVar);
            var df = MathBin<Math_SubNode>(File(target), Get(selFileVar));
            var dr = MathBin<Math_SubNode>(Rank(target), Get(selRankVar));
            var absDf = MathUn<Math_AbsNode>(df);
            var absDr = MathUn<Math_AbsNode>(dr);
            var dfIsZero = MathBin<Math_EqNode>(df, 0);
            var drIsZero = MathBin<Math_EqNode>(dr, 0);

            var targetPiece = GetItem(squarePieceList, target);
            var targetOccupied = MathBin<Math_GeNode>(targetPiece, 0);
            var targetEmpty = MathUn<Math_NotNode>(targetOccupied);
            var ownPieceOnTarget = MathBin<Math_AndNode>(targetOccupied, MathBin<Math_EqNode>(PieceColor(targetPiece), Get(selColorVar)));

            // Pawn: one forward onto an empty square, two from the start rank over an empty square,
            // or one diagonally forward onto an (enemy) piece.
            var isWhite = MathBin<Math_EqNode>(Get(selColorVar), (int) ChessColor.White);
            var forward = Select(isWhite, 1, -1);
            var drIsForward = MathBin<Math_EqNode>(dr, forward);
            var single = AndAll(dfIsZero, drIsForward, targetEmpty);
            var skipped = MathBin<Math_SubNode>(target, MathBin<Math_MulNode>(forward, 8));
            var skippedEmpty = MathBin<Math_LtNode>(GetItem(squarePieceList, skipped), 0);
            var onStartRank = MathBin<Math_EqNode>(Get(selRankVar), Select(isWhite, 1, 6));
            var twoForward = MathBin<Math_EqNode>(dr, MathBin<Math_MulNode>(forward, 2));
            var @double = AndAll(dfIsZero, twoForward, onStartRank, targetEmpty, skippedEmpty);
            var diagonalCapture = AndAll(MathBin<Math_EqNode>(absDf, 1), drIsForward, targetOccupied);
            var pawn = OrAll(single, @double, diagonalCapture);

            var knight = MathBin<Math_OrNode>(
                MathBin<Math_AndNode>(MathBin<Math_EqNode>(absDf, 1), MathBin<Math_EqNode>(absDr, 2)),
                MathBin<Math_AndNode>(MathBin<Math_EqNode>(absDf, 2), MathBin<Math_EqNode>(absDr, 1)));

            // Lines: the n-1 squares before the target must be free in that direction.
            var distance = MathBin<Math_MaxNode>(absDf, absDr);
            var direction = MathBin<Math_AddNode>(
                MathBin<Math_MulNode>(MathBin<Math_AddNode>(MathUn<Math_SignNode>(df), 1), 3),
                MathBin<Math_AddNode>(MathUn<Math_SignNode>(dr), 1));
            var pathClear = MathBin<Math_LeNode>(MathBin<Math_SubNode>(distance, 1), GetItem(freeStepsList, direction));
            var bishop = AndAll(MathBin<Math_EqNode>(absDf, absDr), MathUn<Math_NotNode>(dfIsZero), pathClear);
            var rook = MathBin<Math_AndNode>(MathBin<Math_XorNode>(dfIsZero, drIsZero), pathClear);
            var queen = MathBin<Math_OrNode>(bishop, rook);

            var king = AndAll(MathBin<Math_LeNode>(absDf, 1), MathBin<Math_LeNode>(absDr, 1), MathUn<Math_NotNode>(MathBin<Math_AndNode>(dfIsZero, drIsZero)));

            var byType = export.CreateNode<Math_SwitchNode>();
            byType.Configuration[Math_SwitchNode.IdConfigCases].Value = new[] {0, 1, 2, 3, 4, 5};
            byType.ValueIn(Math_SwitchNode.IdSelection).ConnectToSource(Get(selTypeVar)).SetType(TypeRestriction.LimitToInt);
            byType.ValueIn(Math_SwitchNode.IdDefaultValue).SetValue(false);
            byType.ValueIn(((int) ChessPieceType.Pawn).ToString()).ConnectToSource(pawn);
            byType.ValueIn(((int) ChessPieceType.Knight).ToString()).ConnectToSource(knight);
            byType.ValueIn(((int) ChessPieceType.Bishop).ToString()).ConnectToSource(bishop);
            byType.ValueIn(((int) ChessPieceType.Rook).ToString()).ConnectToSource(rook);
            byType.ValueIn(((int) ChessPieceType.Queen).ToString()).ConnectToSource(queen);
            byType.ValueIn(((int) ChessPieceType.King).ToString()).ConnectToSource(king);

            return MathBin<Math_AndNode>(byType.ValueOut(Math_SwitchNode.IdOut), MathUn<Math_NotNode>(ownPieceOnTarget));
        }

        /// <summary>
        /// For each of the 9 direction indices d (dx = d / 3 - 1, dy = d % 3 - 1) walks from the
        /// selected piece and stores the number of free squares until the first piece or the board
        /// edge in <see cref="freeStepsList"/>[d].
        /// </summary>
        FlowInRef BuildFreeStepsWalk(FlowInRef continueTo)
        {
            var directions = CreateForLoop(0, 9, out var d, out var directionBody, out var directionsDone);
            directionsDone.ConnectToFlowDestination(continueTo);

            var reset = SetVars(out var resetOut, (walkFreeVar, 0), (walkBlockedVar, false));
            directionBody.ConnectToFlowDestination(reset);

            var steps = CreateForLoop(1, 8, out var k, out var stepBody, out var stepsDone);
            resetOut.ConnectToFlowDestination(steps);

            var dx = MathBin<Math_SubNode>(MathBin<Math_DivNode>(d, 3), 1);
            var dy = MathBin<Math_SubNode>(MathBin<Math_RemNode>(d, 3), 1);
            var file = MathBin<Math_AddNode>(Get(selFileVar), MathBin<Math_MulNode>(k, dx));
            var rank = MathBin<Math_AddNode>(Get(selRankVar), MathBin<Math_MulNode>(k, dy));
            var onBoard = AndAll(MathBin<Math_GeNode>(file, 0), MathBin<Math_LtNode>(file, 8), MathBin<Math_GeNode>(rank, 0), MathBin<Math_LtNode>(rank, 8));
            var square = MathBin<Math_AddNode>(MathBin<Math_MulNode>(rank, 8), file);
            var free = MathBin<Math_AndNode>(onBoard, MathBin<Math_LtNode>(GetItem(squarePieceList, square), 0));

            var stillWalking = Branch(MathUn<Math_NotNode>(Get(walkBlockedVar)));
            stepBody.ConnectToFlowDestination(stillWalking.FlowIn(Flow_BranchNode.IdFlowIn));
            var isFree = Branch(free);
            stillWalking.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(isFree.FlowIn(Flow_BranchNode.IdFlowIn));
            isFree.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(SetVars(out _, (walkFreeVar, k)));
            isFree.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(SetVars(out _, (walkBlockedVar, true)));

            stepsDone.ConnectToFlowDestination(SetItem(freeStepsList, d, Get(walkFreeVar), out _));

            return directions;
        }

        // ------------------------------------------------------------------------------------
        // Board colors
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Recolors all 64 squares (one `flow/for`, one material `pointer/set` with the material
        /// index from a lookup):
        /// - legal move of the selected piece: <see cref="LegalMoveSquareColor"/>
        /// - piece of the player to move: <see cref="MovablePieceSquareColor"/> (while a piece is
        ///   selected only its own square; after the game <see cref="WinnerSquareColor"/>)
        /// - otherwise the square's original color.
        /// With a selection, the free steps per direction are computed first (used by the legality).
        /// </summary>
        FlowInRef BuildBoardRefresh(ValueOutRef legal)
        {
            var colorLoop = CreateForLoop(0, 64, out var index, out var body, out _);

            var square = Get(evalSquareVar);
            var piece = GetItem(squarePieceList, square);
            var holdsPieceToMove = MathBin<Math_AndNode>(MathBin<Math_GeNode>(piece, 0), MathBin<Math_EqNode>(PieceColor(piece), Get(turnVar)));
            var hasSelection = Get(hasSelectionVar);
            var showGold = MathBin<Math_AndNode>(holdsPieceToMove,
                MathBin<Math_OrNode>(MathUn<Math_NotNode>(hasSelection), MathBin<Math_EqNode>(square, Get(selSquareVar))));
            var pieceColor = Select(Get(gameOverVar), (Vector4) WinnerSquareColor.linear, (Vector4) MovablePieceSquareColor.linear);

            // baseColorFactor is linear (the material exporter writes color.linear too).
            var defaultColors = squares.Select(s => (Vector4) s.GetComponent<MeshRenderer>().sharedMaterial.color.linear).ToArray();
            object defaultColor = defaultColors.Distinct().Count() == 1 ? defaultColors[0] : Lookup(square, defaultColors, defaultColors[0]);
            var idleColor = Select(showGold, pieceColor, defaultColor);
            var color = Select(MathBin<Math_AndNode>(hasSelection, legal), (Vector4) LegalMoveSquareColor.linear, idleColor);

            var setSquare = SetVars(out var setSquareOut, (evalSquareVar, index));
            body.ConnectToFlowDestination(setSquare);

            var setColor = export.CreateNode<Pointer_SetNode>();
            PointersHelper.AddPointerConfig(setColor, PointersHelper.IdPointerTemplMaterialByIndex + "/pbrMetallicRoughness/baseColorFactor", GltfTypes.Float4);
            PointersHelper.AddPointerTemplateValueInput(setColor, PointersHelper.IdPointerMaterialIndex);
            Bind(setColor.ValueIn(PointersHelper.IdPointerMaterialIndex), Lookup(square, squareMaterials, 0));
            setColor.ValueIn(Pointer_SetNode.IdValue).ConnectToSource(color);
            setSquareOut.ConnectToFlowDestination(setColor.FlowIn(Pointer_SetNode.IdFlowIn));

            var withSelection = Branch(Get(hasSelectionVar));
            withSelection.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(BuildFreeStepsWalk(colorLoop));
            withSelection.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(colorLoop);
            return withSelection.FlowIn(Flow_BranchNode.IdFlowIn);
        }

        // ------------------------------------------------------------------------------------
        // Move
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Moves the selected piece to <see cref="clickedSquareVar"/> (already known to be legal):
        /// snapshot -> board state -> capture (if any) -> start animation -> events -> next turn ->
        /// clear selection -> refresh board. The animation runs in the background; input stays
        /// locked (<see cref="animatingVar"/>) until the moving and the captured piece have landed.
        /// </summary>
        FlowInRef BuildMove(FlowInRef refreshEntry)
        {
            var target = Get(clickedSquareVar);
            var selected = Get(selPieceVar);
            var snapshot = SetVars(out var snapshotOut,
                (mvPieceVar, selected), (mvFromVar, Get(selSquareVar)), (mvToVar, target), (mvCapturedVar, GetItem(squarePieceList, target)),
                (mvStartXVar, GridCoordinate(Get(selFileVar), Get(selRankVar), 0)), (mvStartZVar, GridCoordinate(Get(selFileVar), Get(selRankVar), 2)),
                (mvTargetXVar, GridCoordinate(File(target), Rank(target), 0)), (mvTargetZVar, GridCoordinate(File(target), Rank(target), 2)),
                (animMoverNodeVar, PieceNode(selected)), (animMoverYVar, PieceHeight(selected)));

            var resetScale = SetPieceScale(Get(mvPieceVar), 1f, out var resetScaleOut);
            snapshotOut.ConnectToFlowDestination(resetScale);

            // Board state
            var vacate = SetItem(squarePieceList, Get(mvFromVar), -1, out var vacateOut);
            var occupy = SetItem(squarePieceList, Get(mvToVar), Get(mvPieceVar), out var occupyOut);
            var track = SetItem(pieceSquareList, Get(mvPieceVar), Get(mvToVar), out var trackOut);
            resetScaleOut.ConnectToFlowDestination(vacate);
            vacateOut.ConnectToFlowDestination(occupy);
            occupyOut.ConnectToFlowDestination(track);

            // Capture
            var isCapture = Branch(MathBin<Math_GeNode>(Get(mvCapturedVar), 0));
            trackOut.ConnectToFlowDestination(isCapture.FlowIn(Flow_BranchNode.IdFlowIn));
            var startAnimation = SetVars(out var lockedOut, (animatingVar, true));
            isCapture.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(startAnimation);
            isCapture.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(BuildCapture(startAnimation));

            // Animation: moving piece -> (captured piece to the capture zone) -> unlock
            var unlock = SetVars(out _, (animatingVar, false));
            var capturedHop = BuildHop(animCapturedNodeVar, animCapturedYVar, mvTargetXVar, mvTargetZVar, capTargetXVar, capTargetZVar,
                CaptureHopHeight, captureZoneHeightOffset, CaptureDuration, unlock, out _);
            var afterLanding = Branch(MathBin<Math_GeNode>(Get(mvCapturedVar), 0));
            afterLanding.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(capturedHop);
            afterLanding.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(unlock);
            var moverHop = BuildHop(animMoverNodeVar, animMoverYVar, mvStartXVar, mvStartZVar, mvTargetXVar, mvTargetZVar,
                MoveHopHeight, 0f, MoveDuration, afterLanding.FlowIn(Flow_BranchNode.IdFlowIn), out var moverStarted);
            lockedOut.ConnectToFlowDestination(moverHop);

            // Events + next turn (the animation continues in the background)
            var moved = Get(mvPieceVar);
            var sendMoved = Send(movedEvent, ("piece", moved), ("node", PieceNode(moved)), ("pieceType", PieceType(moved)), ("color", PieceColor(moved)),
                ("fromFile", File(Get(mvFromVar))), ("fromRank", Rank(Get(mvFromVar))), ("toFile", File(Get(mvToVar))), ("toRank", Rank(Get(mvToVar))),
                ("capturedPiece", Get(mvCapturedVar)));
            moverStarted.ConnectToFlowDestination(sendMoved.FlowIn(Event_SendNode.IdFlowIn));

            var clearSelection = SetVars(out var clearedOut, (hasSelectionVar, false), (selPieceVar, -1));
            clearedOut.ConnectToFlowDestination(refreshEntry);

            // King taken: the turn stays with the winner, whose squares the refresh paints in the winner color.
            var gameOver = Branch(Get(gameOverVar));
            sendMoved.FlowOut(Event_SendNode.IdFlowOut).ConnectToFlowDestination(gameOver.FlowIn(Flow_BranchNode.IdFlowIn));
            var sendGameOver = Send(gameOverEvent, ("winner", Get(turnVar)));
            gameOver.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(sendGameOver.FlowIn(Event_SendNode.IdFlowIn));
            sendGameOver.FlowOut(Event_SendNode.IdFlowOut).ConnectToFlowDestination(clearSelection);

            var nextTurn = SetVars(out var nextTurnOut, (turnVar, MathBin<Math_SubNode>(1, Get(turnVar))));
            gameOver.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(nextTurn);
            var sendTurn = Send(turnEvent, ("color", Get(turnVar)));
            nextTurnOut.ConnectToFlowDestination(sendTurn.FlowIn(Event_SendNode.IdFlowIn));
            sendTurn.FlowOut(Event_SendNode.IdFlowOut).ConnectToFlowDestination(clearSelection);

            return snapshot;
        }

        /// <summary>
        /// The piece in <see cref="mvCapturedVar"/> leaves the game (square -1, not selectable),
        /// gets the next free slot of its color's capture zone and ends the game if it's a king.
        /// </summary>
        FlowInRef BuildCapture(FlowInRef continueTo)
        {
            var captured = Get(mvCapturedVar);
            var remove = SetItem(pieceSquareList, captured, -1, out var removeOut);

            var unselectable = export.CreateNode<Pointer_SetNode>();
            PointersHelper.AddPointerConfig(unselectable, PointersHelper.IdPointerSelectability, GltfTypes.Bool);
            PointersHelper.AddPointerTemplateValueInput(unselectable, PointersHelper.IdPointerNodeIndex);
            Bind(unselectable.ValueIn(PointersHelper.IdPointerNodeIndex), PieceNode(captured));
            unselectable.ValueIn(Pointer_SetNode.IdValue).SetValue(false);
            removeOut.ConnectToFlowDestination(unselectable.FlowIn(Pointer_SetNode.IdFlowIn));

            var isWhite = MathBin<Math_EqNode>(PieceColor(captured), (int) ChessColor.White);
            var whiteCount = Get(capturedCountVar[(int) ChessColor.White]);
            var blackCount = Get(capturedCountVar[(int) ChessColor.Black]);
            ValueOutRef Slot(int axis) => Select(isWhite,
                Lookup(whiteCount, CaptureSlots(ChessColor.White, axis), CaptureSlots(ChessColor.White, axis).Last()),
                Lookup(blackCount, CaptureSlots(ChessColor.Black, axis), CaptureSlots(ChessColor.Black, axis).Last()));
            var assignSlot = SetVars(out var assignOut,
                (capTargetXVar, Slot(0)), (capTargetZVar, Slot(2)),
                (animCapturedNodeVar, PieceNode(captured)), (animCapturedYVar, PieceHeight(captured)),
                (capturedCountVar[(int) ChessColor.White], MathBin<Math_AddNode>(whiteCount, Select(isWhite, 1, 0))),
                (capturedCountVar[(int) ChessColor.Black], MathBin<Math_AddNode>(blackCount, Select(isWhite, 0, 1))),
                // No check/checkmate detection, so taking the king is how a game ends.
                (gameOverVar, MathBin<Math_EqNode>(PieceType(captured), (int) ChessPieceType.King)));
            unselectable.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(assignSlot);

            var send = Send(capturedEvent, ("piece", captured), ("node", PieceNode(captured)), ("pieceType", PieceType(captured)), ("color", PieceColor(captured)));
            assignOut.ConnectToFlowDestination(send.FlowIn(Event_SendNode.IdFlowIn));
            send.FlowOut(Event_SendNode.IdFlowOut).ConnectToFlowDestination(continueTo);
            return remove;
        }

        /// <summary>
        /// x or z (by <paramref name="axis"/>) of every capture zone slot of <paramref name="color"/>
        /// (pieces' parent space). White: beside the a-file, filled from rank 1 upwards; black: beside
        /// the h-file, filled from rank 8 downwards; every <see cref="CaptureSlotsPerRow"/> slots a
        /// new row starts one square further out.
        /// </summary>
        float[] CaptureSlots(ChessColor color, int axis)
        {
            return Enumerable.Range(0, MaxCapturedPerColor).Select(slot =>
            {
                var column = slot % CaptureSlotsPerRow;
                var row = slot / CaptureSlotsPerRow;
                var file = color == ChessColor.White ? -captureZoneDistance - row : 7f + captureZoneDistance + row;
                var rank = color == ChessColor.White ? column : 7f - column;
                return (gridOrigin + gridFileStep * file + gridRankStep * rank)[axis];
            }).ToArray();
        }

        // ------------------------------------------------------------------------------------
        // Hop animation
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Animates the node in <paramref name="nodeVar"/> from (startX, startZ) to (endX, endZ)
        /// (variables, pieces' parent space) as a hop: chained linear `pointer/interpolate`s on
        /// `/nodes/{nodeIndex}/translation` through <see cref="MoveSegments"/> waypoints. Waypoint k
        /// (time fraction t = k / MoveSegments) lies smoothstep(t) of the way along (horizontal
        /// ease-in-out, no stop in between) and 4t(1-t) * hop height higher (parabola); the height
        /// blends from the node's height (<paramref name="heightVar"/>) to that plus
        /// <paramref name="endHeightOffset"/>. <paramref name="started"/> fires right away,
        /// <paramref name="landedEntry"/> when the node has landed (also if a viewer can't
        /// interpolate - the node then snaps to the end).
        /// </summary>
        FlowInRef BuildHop(int nodeVar, int heightVar, int startXVar, int startZVar, int endXVar, int endZVar,
            float hopHeightInSquares, float endHeightOffset, float duration, FlowInRef landedEntry, out FlowOutRef started)
        {
            ValueOutRef Flat(int xVar, int zVar)
            {
                var combine = export.CreateNode<Math_Combine3Node>();
                combine.ValueIn(Math_Combine3Node.IdValueA).ConnectToSource(Get(xVar));
                combine.ValueIn(Math_Combine3Node.IdValueB).ConnectToSource(Get(heightVar));
                combine.ValueIn(Math_Combine3Node.IdValueC).ConnectToSource(Get(zVar));
                var value = combine.ValueOut(Math_Combine3Node.IdOut);
                if (export.Context.addUnityGltfSpaceConversion)
                    SpaceConversionHelpers.AddSpaceConversion(export, value, out value);
                return value;
            }

            var start = Flat(startXVar, startZVar);
            var delta = MathBin<Math_SubNode>(Flat(endXVar, endZVar), start);
            var hopHeight = hopHeightInSquares * squareSize;

            var snap = CreateTranslationNode<Pointer_SetNode>(nodeVar);
            snap.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(landedEntry);

            GltfInteractivityExportNode first = null, previous = null;
            for (var k = 1; k <= MoveSegments; k++)
            {
                var t = k / (float) MoveSegments;
                var along = t * t * (3f - 2f * t);
                // The space conversion only flips x, so heights can be added in glTF space.
                var lift = new Vector3(0f, 4f * t * (1f - t) * hopHeight + endHeightOffset * along, 0f);
                var waypoint = MathBin<Math_AddNode>(MathBin<Math_AddNode>(start, MathBin<Math_MulNode>(delta, Vector3.one * along)), lift);

                var segment = CreateTranslationNode<Pointer_InterpolateNode>(nodeVar);
                segment.ValueIn(Pointer_InterpolateNode.IdValue).ConnectToSource(waypoint);
                segment.ValueIn(Pointer_InterpolateNode.IdDuration).SetValue(duration / MoveSegments);
                // Control points on the diagonal = linear easing.
                segment.ValueIn(Pointer_InterpolateNode.IdPoint1).SetValue(new Vector2(1f / 3f, 1f / 3f));
                segment.ValueIn(Pointer_InterpolateNode.IdPoint2).SetValue(new Vector2(2f / 3f, 2f / 3f));
                segment.FlowOut(Pointer_InterpolateNode.IdFlowOutError).ConnectToFlowDestination(snap.FlowIn(Pointer_SetNode.IdFlowIn));

                if (previous == null)
                    first = segment;
                else
                    previous.FlowOut(Pointer_InterpolateNode.IdFlowOutDone).ConnectToFlowDestination(segment.FlowIn(Pointer_InterpolateNode.IdFlowIn));
                previous = segment;

                if (k == MoveSegments)
                    snap.ValueIn(Pointer_SetNode.IdValue).ConnectToSource(waypoint);
            }
            previous.FlowOut(Pointer_InterpolateNode.IdFlowOutDone).ConnectToFlowDestination(landedEntry);

            started = first.FlowOut(Pointer_InterpolateNode.IdFlowOut);
            return first.FlowIn(Pointer_InterpolateNode.IdFlowIn);
        }

        /// <summary>A pointer node on `/nodes/{nodeIndex}/translation`, node index read from <paramref name="nodeVar"/>.</summary>
        GltfInteractivityExportNode CreateTranslationNode<T>(int nodeVar) where T : GltfInteractivityNodeSchema, new()
        {
            var node = export.CreateNode<T>();
            PointersHelper.AddPointerConfig(node, PointersHelper.IdPointerTemplNodeByIndex + "/translation", GltfTypes.Float3);
            PointersHelper.AddPointerTemplateValueInput(node, PointersHelper.IdPointerNodeIndex);
            node.ValueIn(PointersHelper.IdPointerNodeIndex).ConnectToSource(Get(nodeVar));
            return node;
        }

        // ------------------------------------------------------------------------------------
        // Small node-graph helpers
        // ------------------------------------------------------------------------------------

        ValueOutRef Get(int varId)
        {
            VariablesHelpers.GetVariable(export, varId, out var value);
            return value;
        }

        /// <summary>
        /// One `variable/set` for several variables. All values are evaluated before any variable
        /// is written, so this doubles as an atomic snapshot.
        /// </summary>
        FlowInRef SetVars(out FlowOutRef flowOut, params (int varId, object value)[] assignments)
        {
            var node = export.CreateNode<Variable_SetNode>();
            node.Configuration[Variable_SetNode.IdConfigVarIndices].Value = assignments.Select(a => a.varId).ToArray();
            foreach (var (varId, value) in assignments)
            {
                var input = node.ValueIn(varId.ToString());
                Bind(input, value);
                input.SetType(TypeRestriction.LimitToType(export.Context.variables[varId].Type));
            }
            flowOut = node.FlowOut(Variable_SetNode.IdFlowOut);
            return node.FlowIn(Variable_SetNode.IdFlowIn);
        }

        GltfInteractivityExportNode Branch(ValueOutRef condition)
        {
            var node = export.CreateNode<Flow_BranchNode>();
            node.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(condition);
            return node;
        }

        FlowInRef CreateForLoop(int startIndex, int endIndex, out ValueOutRef index, out FlowOutRef body, out FlowOutRef completed)
        {
            var node = export.CreateNode<Flow_ForLoopNode>();
            node.Configuration[Flow_ForLoopNode.IdConfigInitialIndex].Value = startIndex;
            node.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(startIndex);
            node.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(endIndex);
            index = node.ValueOut(Flow_ForLoopNode.IdIndex);
            body = node.FlowOut(Flow_ForLoopNode.IdLoopBody);
            completed = node.FlowOut(Flow_ForLoopNode.IdCompleted);
            return node.FlowIn(Flow_ForLoopNode.IdFlowIn);
        }

        static void Bind(ValueInRef input, object source)
        {
            if (source is ValueOutRef valueOutRef)
                input.ConnectToSource(valueOutRef);
            else
                input.SetValue(source);
        }

        ValueOutRef MathBin<T>(object a, object b) where T : GltfInteractivityNodeSchema, new()
        {
            var node = export.CreateNode<T>();
            Bind(node.ValueIn("a"), a);
            Bind(node.ValueIn("b"), b);
            return node.ValueOut("value");
        }

        ValueOutRef MathUn<T>(object a) where T : GltfInteractivityNodeSchema, new()
        {
            var node = export.CreateNode<T>();
            Bind(node.ValueIn("a"), a);
            return node.ValueOut("value");
        }

        ValueOutRef IntToFloat(object value)
        {
            var node = export.CreateNode<Type_IntToFloatNode>();
            Bind(node.ValueIn(Type_IntToFloatNode.IdInputA), value);
            return node.ValueOut(Type_IntToFloatNode.IdValueResult);
        }

        ValueOutRef Select(object condition, object a, object b)
        {
            var node = export.CreateNode<Math_SelectNode>();
            Bind(node.ValueIn(Math_SelectNode.IdCondition), condition);
            Bind(node.ValueIn(Math_SelectNode.IdValueA), a);
            Bind(node.ValueIn(Math_SelectNode.IdValueB), b);
            return node.ValueOut(Math_SelectNode.IdOutValue);
        }

        ValueOutRef AndAll(params ValueOutRef[] operands) => operands.Skip(1).Aggregate(operands[0], (acc, x) => MathBin<Math_AndNode>(acc, x));
        ValueOutRef OrAll(params ValueOutRef[] operands) => operands.Skip(1).Aggregate(operands[0], (acc, x) => MathBin<Math_OrNode>(acc, x));
    }
}
