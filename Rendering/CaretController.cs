using Parser;

namespace Rendering
{
    public class CaretController : ICaretController
    {
        public int Row { get; private set; }
        public int Col { get; private set; }
        public bool Visible { get; private set; }

        public void SetCaretPosition(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public void MoveCaret(int rowDelta, int colDelta)
        {
            Row += rowDelta;
            Col += colDelta;
        }

        public void Show() => Visible = true;
        public void Hide() => Visible = false;
    }
}
