using UnityEngine;

namespace Khronos_Test_Export
{
    public class TestLayout
    {
        public float coloumnSpaceWidth = 10;
        public Vector2 currentPosition;
        public float currentRowHeight = 0;
        public float labelOffset = 0.6f;
        
        public int maxRows = 15;
        public int currentRow { get; private set; } = 0;
        
        public float maxReservedWidth { get; private set; } = 0f;
        
        private float rowStartX = 0;
        
        public void NextRow()
        {
            if (currentRow >= maxRows)
            {
                rowStartX = maxReservedWidth;
                currentRow = 0;
                currentPosition.y = 0;
                currentRowHeight = 0;
            }

            currentPosition.y -= currentRowHeight + currentRowHeight;
            currentPosition.x = rowStartX;
            currentRowHeight = 0;
            currentRow++;
            
        }
        
        public void AddHorizontalSpace()
        {
            currentPosition.x += coloumnSpaceWidth;
        }

        public Vector2 CurrentLabelPosition()
        {
            return new Vector2(rowStartX + labelOffset, currentPosition.y);
        }
        
        public Vector2 ReserveSpace(Vector2 size)
        {
            var reservedPosition = currentPosition;
            
            currentPosition.x += size.x + coloumnSpaceWidth;
            currentRowHeight = Mathf.Max(currentRowHeight, size.y);
            
            if (size.x < 0)
                maxReservedWidth = Mathf.Min(maxReservedWidth, currentPosition.x + size.x * 2f);
            else
                maxReservedWidth = Mathf.Max(maxReservedWidth, currentPosition.x + size.x * 2f);
            return reservedPosition;
        }

    }
}