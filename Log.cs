﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogRecord
{
	class Log : IDisposable, IAsyncDisposable
	{
		/// <summary>
		/// ファイルに書き込むログの内容
		/// using System.Collections.Concurrent;
		/// </summary>
		private BlockingCollection<string> msg = new();

		/// <summary>
		/// ログTask終了命令用
		/// </summary>
		private CancellationTokenSource tokenSource = new();

		/// <summary>
		/// 書込みTaskの実体(Task終了検知)
		/// </summary>
		private Task taskWait;

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="filename">保存するファイル名</param>
		/// <param name="message">追加するログ(必須ではない)</param>
		internal Log(string filename, string message = "")
		{
			// 例外を出さないようにシフトJISのEncodingを取得
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			// BlockingCollectionを監視しログを書き込むTaskを動作
			logWrite(AppDomain.CurrentDomain.BaseDirectory + filename);
			// 2番目の引数があればログを書き込む
			if (message != "")
			{
				Message(message);
			}
		}

		/// <summary>
		/// デストラクタ
		/// もしかしてDisposeが実行し忘れてるかもしれないので念のため
		/// </summary>
		~Log()
		{
			tokenSource.Cancel();
			taskWait.Wait();
			tokenSource = null;
			msg.Dispose();
			msg = null;
		}

		/// <summary>
		/// 終了時の処理
		/// </summary>
		public async void Dispose()
		{
			while (msg.Count != 0)
			{
				await Task.Delay(10);
			}
			tokenSource.Cancel();
			taskWait.Wait();
			tokenSource = null;
			msg.Dispose();
			msg = null;
			// これでデストラクタは呼ばれなくなるので無駄な終了処理がなくなる
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// 非同期な終了の処理
		/// await using で使用
		/// </summary>
		/// <returns></returns>
		public async ValueTask DisposeAsync()
		{
			while (msg.Count != 0)
			{
				await Task.Delay(10);
			}
			tokenSource.Cancel();
			taskWait.Wait();
			tokenSource = null;
			msg.Dispose();
			msg = null;
			// これでデストラクタは呼ばれなくなるので無駄な終了処理がなくなる
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// ログの追加
		/// </summary>
		/// <param name="message">書き込むメッセージ</param>
		internal void Message(string message)
		{
			_ = msg.TryAdd($"{DateTime.Now:yyyy/M/d HH:mm:ss,}{message}", Timeout.Infinite);
		}

		/// <summary>
		///  ログを記録
		/// </summary>
		/// <param name="message">エラー内容</param>
		private async void logWrite(string fileName)
		{
			// 最初にファイルの有無を確認
			if (File.Exists(fileName) == false)
			{
				// ファイルが存在してなければ作る
				await using var hStream = File.Create(fileName);
				// 作成時に返される FileStream を利用して閉じる
				hStream.Close();
			}

			taskWait = Task.Run(async () =>
			{
				while (true)
				{
					try
					{
						_ = msg.TryTake(out string log, -1, tokenSource.Token);
						while (true)
						{
							try
							{
								// ファイルがロックされている場合例外が発生して以下の処理は行わずリトライとなる
								await using (var stream = new FileStream(fileName, FileMode.Open)) { }
								// ログ書き込み
								await using var fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
								await using var sw = new StreamWriter(fs, Encoding.GetEncoding("Shift-JIS"));
								sw.WriteLine(log);
								break;
							}
							catch
							{
								await Task.Delay(1000);
							}
						}
					}
					catch (OperationCanceledException)
					{
						break;
					}
				}
			});
		}
	}
}
