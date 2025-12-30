#nullable disable
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms; // メッセージボックス表示用に追加
using NMeCab;

namespace Metier
{
    public class MeCabManager
    {
        private readonly MeCabTagger _tagger;

        /// <summary>
        /// MeCabが正常に読み込まれ、利用可能かどうかを示します。
        /// </summary>
        public bool IsAvailable { get; }

        public MeCabManager()
        {
            try
            {
                // 辞書のディレクトリパス
                string dicDir = @"dic/ipadic";

                // ここで辞書が見つからないと例外が発生する
                _tagger = MeCabTagger.Create(dicDir);

                // ここまで到達できれば成功
                IsAvailable = true;
            }
            catch (Exception ex)
            {
                // 読み込み失敗時の処理
                _tagger = null;
                IsAvailable = false;

                // ユーザーに通知するが、アプリは終了させない
                MessageBox.Show(
                    $"形態素解析エンジン(MeCab)の初期化に失敗しました。\n" +
                    $"入力補完や品詞分解などの機能は無効化されますが、エディタとしては引き続き使用可能です。\n\n" +
                    $"エラー詳細: {ex.Message}",
                    "Metier - 起動警告",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        /// <summary>
        /// テキストを受け取り、解析結果を「単語(タブ)品詞情報」の文字列リストとして返します。
        /// </summary>
        public string Analyze(string text)
        {
            // MeCabが使えない、またはテキストが空なら何もしない
            if (!IsAvailable || _tagger == null || string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            try
            {
                var nodes = _tagger.Parse(text);
                var sb = new StringBuilder();

                foreach (var node in nodes)
                {
                    if (!string.IsNullOrEmpty(node.Surface))
                    {
                        sb.AppendLine($"{node.Surface}\t{node.Feature}");
                    }
                }
                return sb.ToString();
            }
            catch
            {
                // 解析中の予期せぬエラーも無視して空文字を返す
                return string.Empty;
            }
        }

        /// <summary>
        /// 高度な利用向け：解析結果のノードリストをそのまま返します。
        /// </summary>
        public IEnumerable<MeCabNode> ParseNodes(string text)
        {
            if (!IsAvailable || _tagger == null || string.IsNullOrWhiteSpace(text))
            {
                // IDE0028: コレクションの初期化を簡素化
                return [];
            }

            try
            {
                return _tagger.Parse(text);
            }
            catch
            {
                return [];
            }
        }
    }
}