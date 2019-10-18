namespace Orleans.Http.MediaTypes.Protobuf
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.IO.Pipelines;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PipeStream : Stream
    {
        private readonly PipeWriter? writer;
        private readonly PipeReader? reader;
        private readonly bool ownsPipe;
        private bool readingCompleted;

        internal PipeStream(PipeWriter writer, bool ownsPipe)
        {
            this.writer = writer;
            this.ownsPipe = ownsPipe;
        }

        internal PipeStream(PipeReader reader, bool ownsPipe)
        {
            this.reader = reader;
            this.ownsPipe = ownsPipe;
        }

        internal PipeStream(IDuplexPipe pipe, bool ownsPipe)
        {
            this.writer = pipe.Output;
            this.reader = pipe.Input;
            this.ownsPipe = ownsPipe;
        }

        /// <inheritdoc />
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        public override bool CanRead => !this.IsDisposed && this.reader != null;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => !this.IsDisposed && this.writer != null;

        /// <inheritdoc />
        public override long Length => throw this.ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc />
        public override long Position
        {
            get => throw this.ThrowDisposedOr(new NotSupportedException());
            set => throw this.ThrowDisposedOr(new NotSupportedException());
        }

        internal PipeReader? UnderlyingPipeReader => this.reader;

        internal PipeWriter? UnderlyingPipeWriter => this.writer;

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (this.writer == null)
            {
                throw new NotSupportedException();
            }

            await this.writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw this.ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc />
        public override void SetLength(long value) => throw this.ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (this.writer == null)
            {
                throw new NotSupportedException();
            }

            cancellationToken.ThrowIfCancellationRequested();
            this.writer.Write(buffer.AsSpan(offset, count));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (this.reader == null)
            {
                throw new NotSupportedException();
            }

            if (this.readingCompleted)
            {
                return 0;
            }

            ReadResult readResult = await this.reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return this.ReadHelper(buffer.AsSpan(offset, count), readResult);
        }

#if SPAN_BUILTIN
        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Verify.NotDisposed(this);

            if (this.reader == null)
            {
                throw new NotSupportedException();
            }

            if (this.readingCompleted)
            {
                return 0;
            }

            ReadResult readResult = await this.reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return this.ReadHelper(buffer.Span, readResult);
        }

        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            Verify.NotDisposed(this);
            if (this.reader == null)
            {
                throw new NotSupportedException();
            }

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            ReadResult readResult = this.reader.ReadAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            return this.ReadHelper(buffer, readResult);
        }

        /// <inheritdoc />
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Verify.NotDisposed(this);
            if (this.writer == null)
            {
                throw new NotSupportedException();
            }

            this.writer.Write(buffer.Span);
            return default;
        }

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Verify.NotDisposed(this);
            if (this.writer == null)
            {
                throw new NotSupportedException();
            }

            this.writer.Write(buffer);
        }

#endif

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits

        /// <inheritdoc />
        public override void Flush()
        {
            if (this.writer == null)
            {
                throw new NotSupportedException();
            }

            this.writer.FlushAsync().GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => this.ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => this.WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            this.IsDisposed = true;
            this.reader?.CancelPendingRead();
            this.writer?.CancelPendingFlush();
            if (this.ownsPipe)
            {
                this.reader?.Complete();
                this.writer?.Complete();
            }

            base.Dispose(disposing);
        }

        private T ReturnIfNotDisposed<T>(T value)
        {
            return value;
        }

        private Exception ThrowDisposedOr(Exception ex)
        {
            throw ex;
        }

        private int ReadHelper(Span<byte> buffer, ReadResult readResult)
        {
            if (readResult.IsCanceled && this.IsDisposed)
            {
                return 0;
            }

            long bytesToCopyCount = Math.Min(buffer.Length, readResult.Buffer.Length);
            ReadOnlySequence<byte> slice = readResult.Buffer.Slice(0, bytesToCopyCount);
            slice.CopyTo(buffer);
            this.reader!.AdvanceTo(slice.End);

            if (readResult.IsCompleted && slice.End.Equals(readResult.Buffer.End))
            {
                this.reader.Complete();
                this.readingCompleted = true;
            }

            return (int)bytesToCopyCount;
        }
    }
}