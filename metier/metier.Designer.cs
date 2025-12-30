namespace Metier
{
    // クラス名が Metier になっていること、partial であることを確認
    partial class Metier
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Metier));
            richTextBox1 = new RichTextBox();
            cursorBox = new PictureBox();
            timer1 = new System.Windows.Forms.Timer(components);
            ((System.ComponentModel.ISupportInitialize)cursorBox).BeginInit();
            SuspendLayout();
            // 
            // richTextBox1
            // 
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Location = new Point(0, 0);
            richTextBox1.Margin = new Padding(5, 6, 5, 6);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(1333, 937);
            richTextBox1.TabIndex = 0;
            richTextBox1.Text = "";
            // 
            // cursorBox
            // 
            cursorBox.BackColor = Color.Black;
            cursorBox.Location = new Point(20, 25);
            cursorBox.Margin = new Padding(5, 6, 5, 6);
            cursorBox.Name = "cursorBox";
            cursorBox.Size = new Size(3, 42);
            cursorBox.TabIndex = 1;
            cursorBox.TabStop = false;
            // 
            // timer1
            // 
            timer1.Interval = 10;
            // 
            // Metier
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1333, 937);
            Controls.Add(cursorBox);
            Controls.Add(richTextBox1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5, 6, 5, 6);
            Name = "Metier";
            Text = "metier";
            ((System.ComponentModel.ISupportInitialize)cursorBox).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.PictureBox cursorBox;
        private System.Windows.Forms.Timer timer1;
    }
}