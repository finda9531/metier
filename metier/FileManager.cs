#nullable disable
using System;
using System.IO;
using System.Windows.Forms;

namespace Metier
{
    public class FileManager
    {
        private readonly RichTextBox _richTextBox;
        private readonly string _autoSavePath;

        public FileManager(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;
            // 保存先を「マイドキュメント」の eep_autosave.rtf に設定
            _autoSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "eep_autosave.rtf");
        }

        // 起動時に自動で読み込むメソッド
        public void AutoLoad()
        {
            if (File.Exists(_autoSavePath))
            {
                try
                {
                    _richTextBox.LoadFile(_autoSavePath, RichTextBoxStreamType.RichText);
                    // カーソルをテキストの最後に移動
                    _richTextBox.Select(_richTextBox.TextLength, 0);
                }
                catch { }
            }
        }

        // 終了時に自動で保存するメソッド
        public void AutoSave()
        {
            try
            {
                _richTextBox.SaveFile(_autoSavePath, RichTextBoxStreamType.RichText);
            }
            catch { }
        }
    }
}