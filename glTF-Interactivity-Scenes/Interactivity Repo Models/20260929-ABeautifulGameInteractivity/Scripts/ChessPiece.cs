using UnityEngine;

namespace Chess
{
    public enum ChessPieceType
    {
        Pawn = 0,
        Knight = 1,
        Bishop = 2,
        Rook = 3,
        Queen = 4,
        King = 5,
    }

    public enum ChessColor
    {
        White = 0,
        Black = 1,
    }

    /// <summary>
    /// Marks a glTF node as a chess piece and stores the data the interactivity graph
    /// exporter (<see cref="ChessInteractivityExport"/>) needs at export time.
    /// The piece's live file/rank while playing is tracked at runtime via exported
    /// KHR_interactivity variables, not via this component (this only supplies the
    /// starting position).
    /// </summary>
    public class ChessPiece : MonoBehaviour
    {
        public ChessPieceType type;
        public ChessColor color;

        [Tooltip("0-7, board file the piece starts on")]
        public int startFile;

        [Tooltip("0-7, board rank the piece starts on")]
        public int startRank;
    }
}
