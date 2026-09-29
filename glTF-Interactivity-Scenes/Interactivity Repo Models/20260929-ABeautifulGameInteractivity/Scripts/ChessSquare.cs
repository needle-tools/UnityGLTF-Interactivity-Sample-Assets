using UnityEngine;

namespace Chess
{
    /// <summary>
    /// Marks a glTF node as one clickable square of the 8x8 board.
    /// Needs a mesh (added by ChessSceneBuilder) so glTF viewers have
    /// something to raycast against for KHR_node_selectability/event/onSelect.
    /// </summary>
    public class ChessSquare : MonoBehaviour
    {
        [Tooltip("0-7, a..h")]
        public int file;

        [Tooltip("0-7, 1..8")]
        public int rank;
    }
}
