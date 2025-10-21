using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parser
{
    public interface ICaretController
    {
        void SetCaretPosition(int row, int col);
        void MoveCaret(int dRow, int dCol);
        void Show();
        void Hide();
    }
}
