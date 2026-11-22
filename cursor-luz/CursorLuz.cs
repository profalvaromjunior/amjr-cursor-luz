// AMJR Cursor Luz - Prof. Alvaro Machado Junior
// Deixa o ponteiro do mouse iluminado e mostra uma animacao a cada clique.
// Atalho: Ctrl + Alt + L liga/desliga. Menu completo no icone da bandeja.
// Compativel com o compilador do proprio Windows (csc .NET Framework 4).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AMJR
{
    class Ripple
    {
        public DateTime Start;
        public Color Cor;
    }

    class CursorLuz : Form
    {
        // ---------- Win32 ----------
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

        const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOPMOST = 0x8,
                  WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 1, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;

        // ---------- Estado ----------
        int TAM = 260;                     // tamanho da janela-luz (px), acompanha o tamanho
        const int DURACAO_MS = 550;        // duracao da animacao de clique
        Timer timer = new Timer();
        NotifyIcon tray = new NotifyIcon();
        List<Ripple> ripples = new List<Ripple>();
        bool ligado = true, esqAntes, dirAntes, precisaDesenhar = true;
        int ultX = int.MinValue, ultY = int.MinValue;

        Color corHalo = Color.FromArgb(255, 230, 0);   // amarelo
        int raio = 34;                                  // medio
        int opacidade = 120;                            // 0-255
        bool mostrarCliques = true;

        static readonly Color COR_ESQ = Color.FromArgb(255, 60, 60);   // clique esquerdo: vermelho
        static readonly Color COR_DIR = Color.FromArgb(40, 140, 255);  // clique direito: azul

        string arquivoConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AMJR", "CursorLuz.ini");

        public CursorLuz()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(TAM, TAM);
            Text = "AMJR Cursor Luz";
            CarregarConfig();
            AjustarJanela();
            MontarBandeja();
            timer.Interval = 10;
            timer.Tick += delegate { Atualizar(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotKey(Handle, 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.L);
            timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnregisterHotKey(Handle, 1);
            tray.Visible = false;
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY) Alternar();
            base.WndProc(ref m);
        }

        // ---------- Laco principal ----------
        void Atualizar()
        {
            if (!ligado) return;

            bool esq = (GetAsyncKeyState(0x01) & 0x8000) != 0;
            bool dir = (GetAsyncKeyState(0x02) & 0x8000) != 0;
            if (mostrarCliques)
            {
                if (esq && !esqAntes) NovoRipple(COR_ESQ);
                if (dir && !dirAntes) NovoRipple(COR_DIR);
                if (esq != esqAntes || dir != dirAntes) precisaDesenhar = true;
            }
            esqAntes = esq; dirAntes = dir;

            DateTime agora = DateTime.Now;
            ripples.RemoveAll(delegate(Ripple r) { return (agora - r.Start).TotalMilliseconds > DURACAO_MS; });
            if (ripples.Count > 0) precisaDesenhar = true;

            POINT p; GetCursorPos(out p);
            int x = p.X - TAM / 2, y = p.Y - TAM / 2;

            if (precisaDesenhar)
            {
                Desenhar(x, y, esq && mostrarCliques, dir && mostrarCliques);
                precisaDesenhar = ripples.Count > 0;
                ultX = x; ultY = y;
            }
            else if (x != ultX || y != ultY)
            {
                SetWindowPos(Handle, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                ultX = x; ultY = y;
            }
        }

        // A janela cresce junto com o tamanho escolhido, para as ondas caberem
        void AjustarJanela()
        {
            TAM = Math.Max(260, (int)(raio * 7.5f) + 20);
            Size = new Size(TAM, TAM);
        }

        void NovoRipple(Color c)
        {
            Ripple r = new Ripple(); r.Start = DateTime.Now; r.Cor = c;
            ripples.Add(r);
        }

        void Desenhar(int x, int y, bool esq, bool dir)
        {
            using (Bitmap bmp = new Bitmap(TAM, TAM, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    float c = TAM / 2f;

                    // Halo com brilho suave (gradiente radial)
                    float rg = raio * 1.35f;
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddEllipse(c - rg, c - rg, rg * 2, rg * 2);
                        using (PathGradientBrush pb = new PathGradientBrush(gp))
                        {
                            pb.CenterColor = Color.FromArgb(opacidade, corHalo);
                            pb.SurroundColors = new Color[] { Color.FromArgb(0, corHalo) };
                            ColorBlend cb = new ColorBlend(3);
                            cb.Colors = new Color[] { Color.FromArgb(0, corHalo), Color.FromArgb(opacidade, corHalo), Color.FromArgb(opacidade, corHalo) };
                            cb.Positions = new float[] { 0f, 0.35f, 1f };
                            pb.InterpolationColors = cb;
                            g.FillEllipse(pb, c - rg, c - rg, rg * 2, rg * 2);
                        }
                    }
                    // Contorno fino para dar definicao no projetor
                    using (Pen borda = new Pen(Color.FromArgb(Math.Min(255, opacidade + 70), corHalo), Math.Max(2f, raio / 17f)))
                        g.DrawEllipse(borda, c - raio, c - raio, raio * 2, raio * 2);

                    // Botao pressionado (bom para mostrar "arrastar")
                    if (esq || dir)
                    {
                        Color cc = esq ? COR_ESQ : COR_DIR;
                        float ri = raio * 0.55f;
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(110, cc)))
                            g.FillEllipse(b, c - ri, c - ri, ri * 2, ri * 2);
                    }

                    // Ondas de clique
                    DateTime agora = DateTime.Now;
                    foreach (Ripple r in ripples)
                    {
                        float t = (float)((agora - r.Start).TotalMilliseconds / DURACAO_MS);
                        if (t > 1f) t = 1f;
                        float ease = 1f - (1f - t) * (1f - t);
                        float rr = raio * 0.6f + (TAM / 2f - 8 - raio * 0.6f) * ease;
                        int a = (int)(230 * (1f - t));
                        float esc = Math.Max(1f, raio / 34f);
                        using (Pen pen = new Pen(Color.FromArgb(a, r.Cor), (5f * (1f - t) + 1.5f) * esc))
                            g.DrawEllipse(pen, c - rr, c - rr, rr * 2, rr * 2);
                    }
                }
                AplicarBitmap(bmp, x, y);
            }
        }

        void AplicarBitmap(Bitmap bmp, int x, int y)
        {
            IntPtr tela = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(tela);
            IntPtr hBmp = IntPtr.Zero, antigo = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                antigo = SelectObject(memDC, hBmp);
                SIZE tam = new SIZE(bmp.Width, bmp.Height);
                POINT origem = new POINT(0, 0);
                POINT destino = new POINT(x, y);
                BLENDFUNCTION bf = new BLENDFUNCTION();
                bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
                UpdateLayeredWindow(Handle, tela, ref destino, ref tam, memDC, ref origem, 0, ref bf, 2);
                SetWindowPos(Handle, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, tela);
                if (hBmp != IntPtr.Zero) { SelectObject(memDC, antigo); DeleteObject(hBmp); }
                DeleteDC(memDC);
            }
        }

        // ---------- Liga / desliga ----------
        void Alternar()
        {
            ligado = !ligado;
            if (ligado) { Show(); precisaDesenhar = true; }
            else { Hide(); ripples.Clear(); }
            tray.Text = ligado ? "AMJR Cursor Luz - ligado (Ctrl+Alt+L)" : "AMJR Cursor Luz - desligado (Ctrl+Alt+L)";
            AtualizarMenu();
        }

        // ---------- Bandeja ----------
        ToolStripMenuItem itemLigar, itemCliques, itemIniciar;
        ToolStripMenuItem menuCor, menuTam, menuOpac;

        void MontarBandeja()
        {
            tray.Icon = CriarIcone();
            tray.Text = "AMJR Cursor Luz - ligado (Ctrl+Alt+L)";
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem titulo = new ToolStripMenuItem("AMJR Cursor Luz  -  Prof. Alvaro");
            titulo.Enabled = false;
            menu.Items.Add(titulo);
            menu.Items.Add(new ToolStripSeparator());

            itemLigar = new ToolStripMenuItem("Ligado   (Ctrl+Alt+L)", null, delegate { Alternar(); });
            menu.Items.Add(itemLigar);

            itemCliques = new ToolStripMenuItem("Mostrar cliques", null, delegate
            {
                mostrarCliques = !mostrarCliques; SalvarConfig(); AtualizarMenu(); precisaDesenhar = true;
            });
            menu.Items.Add(itemCliques);

            menuCor = new ToolStripMenuItem("Cor da luz");
            AddCor("Amarelo", Color.FromArgb(255, 230, 0));
            AddCor("Verde-limao", Color.FromArgb(120, 255, 40));
            AddCor("Ciano", Color.FromArgb(0, 230, 255));
            AddCor("Rosa", Color.FromArgb(255, 60, 200));
            AddCor("Laranja", Color.FromArgb(255, 140, 0));
            AddCor("Branco", Color.White);
            menu.Items.Add(menuCor);

            menuTam = new ToolStripMenuItem("Tamanho");
            AddTam("Pequeno", 24); AddTam("Medio", 34); AddTam("Grande", 48); AddTam("Gigante", 64);
            AddTam("Enorme (cursor grande)", 90); AddTam("Maximo (cursor no maximo)", 120);
            menu.Items.Add(menuTam);

            menuOpac = new ToolStripMenuItem("Intensidade");
            AddOpac("Suave", 80); AddOpac("Normal", 120); AddOpac("Forte", 170);
            menu.Items.Add(menuOpac);

            itemIniciar = new ToolStripMenuItem("Iniciar com o Windows", null, delegate
            {
                DefinirInicio(!IniciaComWindows()); AtualizarMenu();
            });
            menu.Items.Add(itemIniciar);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Sair", null, delegate { Close(); }));

            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Alternar(); };
            tray.Visible = true;
            AtualizarMenu();
        }

        void AddCor(string nome, Color cor)
        {
            Bitmap amostra = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(amostra)) { g.Clear(cor); g.DrawRectangle(Pens.Gray, 0, 0, 15, 15); }
            ToolStripMenuItem it = new ToolStripMenuItem(nome, amostra, delegate
            {
                corHalo = cor; SalvarConfig(); AtualizarMenu(); precisaDesenhar = true;
            });
            it.Tag = cor.ToArgb();
            menuCor.DropDownItems.Add(it);
        }

        void AddTam(string nome, int r)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(nome, null, delegate
            {
                raio = r; AjustarJanela(); SalvarConfig(); AtualizarMenu(); precisaDesenhar = true;
            });
            it.Tag = r;
            menuTam.DropDownItems.Add(it);
        }

        void AddOpac(string nome, int o)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(nome, null, delegate
            {
                opacidade = o; SalvarConfig(); AtualizarMenu(); precisaDesenhar = true;
            });
            it.Tag = o;
            menuOpac.DropDownItems.Add(it);
        }

        void AtualizarMenu()
        {
            itemLigar.Checked = ligado;
            itemCliques.Checked = mostrarCliques;
            itemIniciar.Checked = IniciaComWindows();
            foreach (ToolStripMenuItem it in menuCor.DropDownItems) it.Checked = (int)it.Tag == corHalo.ToArgb();
            foreach (ToolStripMenuItem it in menuTam.DropDownItems) it.Checked = (int)it.Tag == raio;
            foreach (ToolStripMenuItem it in menuOpac.DropDownItems) it.Checked = (int)it.Tag == opacidade;
        }

        const string CHAVE_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";

        bool IniciaComWindows()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CHAVE_RUN))
                    return k != null && k.GetValue("AMJR Cursor Luz") != null;
            }
            catch { return false; }
        }

        void DefinirInicio(bool ativar)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CHAVE_RUN))
                {
                    if (ativar) k.SetValue("AMJR Cursor Luz", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("AMJR Cursor Luz", false);
                }
            }
            catch { }
        }

        Icon CriarIcone()
        {
            Bitmap b = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush halo = new SolidBrush(Color.FromArgb(200, 255, 220, 0)))
                    g.FillEllipse(halo, 2, 2, 28, 28);
                Point[] seta = { new Point(11, 7), new Point(11, 25), new Point(15, 21), new Point(18, 27),
                                 new Point(21, 26), new Point(18, 20), new Point(23, 20) };
                g.FillPolygon(Brushes.White, seta);
                using (Pen p = new Pen(Color.Black, 1.5f)) g.DrawPolygon(p, seta);
            }
            return Icon.FromHandle(b.GetHicon());
        }

        // ---------- Configuracao ----------
        void CarregarConfig()
        {
            try
            {
                if (!File.Exists(arquivoConfig)) return;
                foreach (string linha in File.ReadAllLines(arquivoConfig))
                {
                    string[] kv = linha.Split('=');
                    if (kv.Length != 2) continue;
                    int v;
                    if (!int.TryParse(kv[1].Trim(), out v)) continue;
                    switch (kv[0].Trim())
                    {
                        case "cor": corHalo = Color.FromArgb(v); break;
                        case "raio": raio = v; break;
                        case "opacidade": opacidade = v; break;
                        case "cliques": mostrarCliques = v != 0; break;
                    }
                }
            }
            catch { }
        }

        void SalvarConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(arquivoConfig));
                File.WriteAllLines(arquivoConfig, new string[] {
                    "cor=" + corHalo.ToArgb(),
                    "raio=" + raio,
                    "opacidade=" + opacidade,
                    "cliques=" + (mostrarCliques ? 1 : 0)
                });
            }
            catch { }
        }

        // ---------- Inicio ----------
        [STAThread]
        static void Main()
        {
            bool novo;
            using (System.Threading.Mutex mtx = new System.Threading.Mutex(true, "AMJR_CursorLuz_Unico", out novo))
            {
                if (!novo)
                {
                    MessageBox.Show("O AMJR Cursor Luz ja esta aberto.\nProcure o icone perto do relogio.",
                        "AMJR Cursor Luz", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.Run(new CursorLuz());
            }
        }
    }
}
