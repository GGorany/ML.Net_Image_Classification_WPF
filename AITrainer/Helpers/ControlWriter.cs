using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AITrainer.Helpers
{
    public class ControlWriter : TextWriter
    {
        private TextBox textbox;
        Dispatcher dispatcher = Application.Current.Dispatcher;

        public ControlWriter(TextBox textbox)
        {
            this.textbox = textbox;
        }

        public override void Write(char value)
        {
            dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate
            {
                textbox.Text += value;
                textbox.ScrollToEnd();
            }));
        }

        public override void Write(string value)
        {
            dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate
            {
                textbox.Text += value;
                textbox.ScrollToEnd();
            }));
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }
}
