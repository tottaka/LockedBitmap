using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

/// <summary>
    /// Provides managed, direct access to the data in a <see cref="System.Drawing.Bitmap"/>.
    /// </summary>
    public class LockedBitmap : IDisposable
    {
        /// <summary>
        /// The size of this bitmap.
        /// </summary>
        public Size Size { get; private set; }

        /// <summary>
        /// The number of bytes for each pixel in the bitmap.
        /// </summary>
        public int PixelSize { get; private set; }

        /// <summary>
        /// The <see cref="PixelFormat"/> for this <see cref="LockedBitmap"/>'s image data.
        /// </summary>
        public PixelFormat Format { get; private set; }

        /// <summary>
        /// The width (in pixels) of the bitmap.
        /// </summary>
        public int Width => Size.Width;

        /// <summary>
        /// The height (in pixels) of the bitmap.
        /// </summary>
        public int Height => Size.Height;

        /// <summary>
        /// The scan-width of this bitmap.
        /// This is how many bytes are in one row of pixels in the bitmap.
        /// </summary>
        public int Stride => Width * PixelSize;

        /// <summary>
        /// The length (in bytes) of this bitmap.
        /// </summary>
        public long Length => Width * Height * Stride;

        /// <summary>
        /// Whether or not this <see cref="LockedBitmap"/> instance has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// The raw data for this <see cref="LockedBitmap"/> instance.
        /// You can read/write to this data.
        /// </summary>
        public byte[] Bits { get; private set; }

        /// <summary>
        /// An <see cref="IntPtr"/> pointing to the bitmap data for this <see cref="LockedBitmap"/> instance.
        /// This is used as the Scan0 of the underlying bitmap.
        /// </summary>
        public IntPtr BitsPointer { get; private set; }

        /// <summary>
        /// The underlaying <see cref="System.Drawing.Bitmap"/> instance.
        /// Note that the bitmap data is "always locked" and is accessible via <seealso cref="Bits"/> or <seealso cref="BitsPointer"/>.
        /// </summary>
        public Bitmap Bitmap { get; private set; }

        /// <summary>
        /// The <see cref="GCHandle"/> associated with this <see cref="LockedBitmap"/> instance.
        /// </summary>
        protected GCHandle BitsHandle { get; private set; }

        /// <summary>
        /// Constructs a new <see cref="LockedBitmap"/> instance.
        /// </summary>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="format">The <see cref="PixelFormat"/> of the image</param>
        /// <param name="bitmapData">Initial image data. An empty image will be created if left null/unspecified.</param>
        public LockedBitmap(int width, int height, PixelFormat format, byte[] bitmapData = null)
        {
            Size = new Size(width, height);
            Format = format;
            PixelSize = Image.GetPixelFormatSize(format) / 8;
            int imageSize = width * height * PixelSize;

            // add checks here
            if (bitmapData == null)
                Bits = new byte[imageSize];
            else if (bitmapData.Length != imageSize)
                throw new ArgumentException(string.Format("Invalid data length. Expected {0} bytes, got {1}.", imageSize, bitmapData.Length), "bitmapData");
            else
                Bits = bitmapData;

            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            BitsPointer = BitsHandle.AddrOfPinnedObject();
            Bitmap = new Bitmap(Width, Height, Stride, Format, BitsPointer);
        }

        /// <summary>
        /// Constructs a new <see cref="LockedBitmap"/> instance.
        /// </summary>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="format">The <see cref="PixelFormat"/> of the image</param>
        /// <param name="data">Initial image data.</param>
        public LockedBitmap(int width, int height, PixelFormat format, IntPtr data)
        {
            Size = new Size(width, height);
            Format = format;
            PixelSize = Image.GetPixelFormatSize(format) / 8;
            BitsHandle = GCHandle.FromIntPtr(data);
            BitsPointer = BitsHandle.AddrOfPinnedObject();
            Bitmap = new Bitmap(Width, Height, Stride, Format, BitsPointer);
        }

        ~LockedBitmap()
        {
            Dispose(false);
        }

        /// <summary>
        /// Free all resources used by this <see cref="LockedBitmap"/> instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                Bitmap.Dispose();
                BitsHandle.Free();
                BitsPointer = IntPtr.Zero;
                Bits = null;
                IsDisposed = true;
            }
        }

        /// <summary>
        /// Get the pixel at the given x and y coordinates.
        /// </summary>
        public Color GetPixel(int x, int y)
        {
            if (x > Width || x < 0)
                throw new ArgumentOutOfRangeException("x", "Value is out of bounds for this image.");
            if (y > Height || y < 0)
                throw new ArgumentOutOfRangeException("y", "Value is out of bounds for this image.");

            int index = GetByteIndex(x, y);
            return Color.FromArgb(Bits[index], Bits[index + 1], Bits[index + 2], Bits[index + 3]);
        }

        /// <summary>
        /// This needs to be able to support any pixel format, right now it is hard coded for rgba
        /// </summary>
        public void SetPixel(int x, int y, Color color)
        {
            if (x > Width || x < 0)
                throw new ArgumentOutOfRangeException("x", "Value is out of bounds for this image.");
            if (y > Height || y < 0)
                throw new ArgumentOutOfRangeException("y", "Value is out of bounds for this image.");

            int index = GetByteIndex(x, y);
            Bits[index] = color.R;
            Bits[index + 1] = color.G;
            Bits[index + 2] = color.B;
            Bits[index + 3] = color.A;
        }

        /// <summary>
        /// Get the pixel data from the specified region of the image.
        /// </summary>
        public byte[] GetData(Rectangle region)
        {
            if (region.X > Width || region.X < 0)
                throw new ArgumentOutOfRangeException("x", "Value is out of bounds for this image.");
            if (region.Y > Height || region.Y < 0)
                throw new ArgumentOutOfRangeException("y", "Value is out of bounds for this image.");
            if (region.X + region.Width > Width || region.Y + region.Height > Height)
                throw new ArgumentOutOfRangeException("region", "Target region extends beyond the bounds of the original image.");

            int scanWidth = region.Width * PixelSize;
            int regionSize = scanWidth * region.Height;

            byte[] data = new byte[regionSize];

            // now copy the data row by row
            for (int i = 0; i < region.Height; i++)
            {
                int index = GetByteIndex(region.X, region.Y + i);
                Buffer.BlockCopy(Bits, index, data, i * scanWidth, scanWidth);
            }

            return data;
        }

        /// <summary>
        /// BlockCopy the data to the specified region of the image.
        /// </summary>
        public void SetData(Rectangle region, byte[] data)
        {
            if (region.X > Width || region.X < 0)
                throw new ArgumentOutOfRangeException("x", "Value is out of bounds for this image.");
            if (region.Y > Height || region.Y < 0)
                throw new ArgumentOutOfRangeException("y", "Value is out of bounds for this image.");
            if (region.X + region.Width > Width || region.Y + region.Height > Height)
                throw new ArgumentOutOfRangeException("region", "Target region extends beyond the bounds of the original image.");

            // calculate the expected size of the data
            int scanWidth = region.Width * PixelSize;
            int expectedSize = scanWidth * region.Height;
            if (data == null || data.Length != expectedSize)
                throw new ArgumentException(string.Format("The data you are trying to insert is not the right size. Expected {0} bytes, got {1}.", expectedSize, data?.Length ?? 0), "data");

            // now copy the data row by row
            for (int i = 0; i < region.Height; i++)
            {
                int index = GetByteIndex(region.X, region.Y + i);
                Buffer.BlockCopy(data, i * scanWidth, Bits, index, scanWidth);
            }
        }

        /// <summary>
        /// Create a <see cref="System.Drawing.Graphics"/> surface for drawing to this <see cref="LockedBitmap"/> instance using GDI+.
        /// </summary>
        public System.Drawing.Graphics CreateGraphics() => System.Drawing.Graphics.FromImage(Bitmap);
        private int GetByteIndex(int x, int y) => ((y * Width) + x) * PixelSize;

        /// <summary>
        /// Converts this <see cref="LockedBitmap"/> into an unlocked <see cref="Bitmap"/>.
        /// </summary>
        public Bitmap ToBitmap() => ToBitmap(new Rectangle(Point.Empty, Size));
        public Bitmap ToBitmap(Rectangle region)
        {
            if (region.X > Width || region.X < 0)
                throw new ArgumentOutOfRangeException("x", "Value is out of bounds for this image.");
            if (region.Y > Height || region.Y < 0)
                throw new ArgumentOutOfRangeException("y", "Value is out of bounds for this image.");
            if (region.X + region.Width > Width || region.Y + region.Height > Height)
                throw new ArgumentOutOfRangeException("region", "Target region extends beyond the bounds of the original image.");

            Bitmap bmp = new Bitmap(region.Width, region.Height);
            BitmapData bits = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), ImageLockMode.WriteOnly, Format);

            byte[] data = GetData(region);
            Marshal.Copy(data, 0, bits.Scan0, data.Length);

            bmp.UnlockBits(bits);
            return bmp;
        }

        /// <summary>
        /// Copies the specified <see cref="Bitmap"/> to a new <see cref="LockedBitmap"/> instance.
        /// </summary>
        public static LockedBitmap FromBitmap(Bitmap bitmap) => FromBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        public static LockedBitmap FromBitmap(Bitmap bitmap, Rectangle region)
        {
            if (region.X > bitmap.Width || region.X < 0)
                throw new ArgumentOutOfRangeException("x", "Value is out of bounds for this image.");
            if (region.Y > bitmap.Height || region.Y < 0)
                throw new ArgumentOutOfRangeException("y", "Value is out of bounds for this image.");
            if (region.X + region.Width > bitmap.Width || region.Y + region.Height > bitmap.Height)
                throw new ArgumentOutOfRangeException("region", "Target region extends beyond the bounds of the original image.");

            BitmapData bits = bitmap.LockBits(region, ImageLockMode.ReadOnly, bitmap.PixelFormat);
            byte[] bitmapData = new byte[bits.Stride * bits.Height];
            Marshal.Copy(bits.Scan0, bitmapData, 0, bitmapData.Length);
            bitmap.UnlockBits(bits);
            return new LockedBitmap(region.Width, region.Height, bitmap.PixelFormat, bitmapData);
        }

        public static implicit operator Image(LockedBitmap lockedBitmap) => lockedBitmap.Bitmap;
        public static implicit operator LockedBitmap(Image bitmap) => FromBitmap((Bitmap)bitmap);
    }