using System;
using System.Text;

namespace IncludeFixor
{

	/// <summary>
	/// Spreads data out to multiple text writers.
	/// </summary>
	class TextWriterRouter : System.IO.TextWriter
	{
		private System.Collections.Generic.List<System.IO.TextWriter> _writers = new System.Collections.Generic.List<System.IO.TextWriter>();
		private System.IFormatProvider _formatProvider = null;
		private System.Text.Encoding _encoding = null;

		#region TextWriter Properties
		public override System.IFormatProvider FormatProvider
		{
			get
			{
				var formatProvider = this._formatProvider;
				if (formatProvider == null)
				{
					formatProvider = base.FormatProvider;
				}
				return formatProvider;
			}
		}

		public override string NewLine
		{
			get { return base.NewLine; }

			set
			{
				foreach (var writer in this._writers)
				{
					writer.NewLine = value;
				}

				base.NewLine = value;
			}
		}


		public override System.Text.Encoding Encoding
		{
			get
			{
				var encoding = this._encoding;

				if (encoding == null)
				{
					encoding = System.Text.Encoding.Default;
				}

				return encoding;
			}
		}

		#region TextWriterRouter Property Setters

		TextWriterRouter SetFormatProvider(System.IFormatProvider value)
		{
			this._formatProvider = value;
			return this;
		}

		TextWriterRouter SetEncoding(System.Text.Encoding value)
		{
			this._encoding = value;
			return this;
		}
		#endregion // TextWriter Property Setters
		#endregion // TextWriter Properties


		#region Construction/Destruction
		public TextWriterRouter(System.Collections.Generic.IEnumerable<System.IO.TextWriter> writers)
		{
			this.Clear();
			this.AddWriters(writers);
		}
		#endregion // Construction/Destruction

		#region Public interface
		public TextWriterRouter Clear()
		{
			this._writers.Clear();
			return this;
		}

		public TextWriterRouter AddWriter(System.IO.TextWriter writer)
		{
			this._writers.Add(writer);
			return this;
		}

		public TextWriterRouter AddWriters(System.Collections.Generic.IEnumerable<System.IO.TextWriter> writers)
		{
			this._writers.AddRange(writers);
			return this;
		}
		#endregion // Public interface

		#region TextWriter methods

		public override void Close()
		{
			foreach (var writer in this._writers)
			{
				writer.Close();
			}
			base.Close();
		}

		protected override void Dispose(bool disposing)
		{
			foreach (var writer in this._writers)
			{
				if (disposing)
				{
					writer.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		public override void Flush()
		{
			foreach (var writer in this._writers)
			{
				writer.Flush();
			}

			base.Flush();
		}

		//foreach (System.IO.TextWriter writer in this.writers)
		//{
		//    writer;
		//}
		public override void Write(bool value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(char value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(char[] buffer)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(buffer);
			}
		}

		public override void Write(decimal value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(double value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(float value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(int value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(long value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(object value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(string value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(uint value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(ulong value)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(value);
			}
		}

		public override void Write(string format, object arg0)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(format, arg0);
			}

		}

		public override void Write(string format, params object[] arg)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(format, arg);
			}
		}

		public override void Write(char[] buffer, int index, int count)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(buffer, index, count);
			}
		}

		public override void Write(string format, object arg0, object arg1)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(format, arg0, arg1);
			}
		}

		public override void Write(string format, object arg0, object arg1, object arg2)
		{
			foreach (var writer in this._writers)
			{
				writer.Write(format, arg0, arg1, arg2);
			}
		}

		public override void WriteLine()
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine();
			}
		}

		public override void WriteLine(bool value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(char value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(char[] buffer)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(buffer);
			}
		}

		public override void WriteLine(decimal value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(double value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(float value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(int value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(long value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(object value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(string value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(uint value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(ulong value)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(value);
			}
		}

		public override void WriteLine(string format, object arg0)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(format, arg0);
			}
		}

		public override void WriteLine(string format, params object[] arg)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(format, arg);
			}
		}

		public override void WriteLine(char[] buffer, int index, int count)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(buffer, index, count);
			}
		}

		public override void WriteLine(string format, object arg0, object arg1)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(format, arg0, arg1);
			}
		}

		public override void WriteLine(string format, object arg0, object arg1, object arg2)
		{
			foreach (var writer in this._writers)
			{
				writer.WriteLine(format, arg0, arg1, arg2);
			}
		}
		#endregion // TextWriter methods
	}
}
