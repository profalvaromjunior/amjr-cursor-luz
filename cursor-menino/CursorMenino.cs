// AMJR Cursor Menino - Prof. Alvaro Machado Junior
// Troca o ponteiro do Windows pelo menino apontando (animado ou parado).
// Atalho: Ctrl + Alt + M liga/desliga. Menu completo no icone da bandeja.
// Ao sair, o ponteiro original do Windows volta automaticamente.
// Compativel com o compilador do proprio Windows (csc .NET Framework 4).

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AMJR
{
    class CursorMenino : Form
    {
        // ---------- Win32 ----------
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint flags);
        [DllImport("user32.dll", SetLastError = true)] static extern bool SetSystemCursor(IntPtr hcur, uint id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadCursorFromFile(string name);
        [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint winIni);
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        const uint IMAGE_CURSOR = 2, LR_LOADFROMFILE = 0x10;
        const uint OCR_NORMAL = 32512, OCR_HAND = 32649;
        const uint SPI_SETCURSORS = 0x57;
        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;

        // ---------- Estado ----------
        NotifyIcon tray = new NotifyIcon();
        bool ligado = true;
        bool animado = true;
        bool tambemLink = true;    // usa o menino tambem na "maozinha" dos links
        int tamanho = 64;

        string pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AMJR");
        string arqConfig, arqAnimado, arqParado;

        public CursorMenino()
        {
            Text = "AMJR Cursor Menino";
            ShowInTaskbar = false;
            arqConfig = Path.Combine(pasta, "CursorMenino.ini");
            arqAnimado = Path.Combine(pasta, "menino_animado.ani");
            arqParado = Path.Combine(pasta, "menino_parado.cur");
            ExtrairArquivos();
            CarregarConfig();
            MontarBandeja();
            if (LerConfig("origArrow") != null) Restaurar();   // sobra de uma sessao anterior
            if (ligado) Aplicar();
        }

        // Janela invisivel: so existe para receber o atalho de teclado
        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated) { CreateHandle(); value = false; }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotKey(Handle, 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.M);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY) Alternar();
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnregisterHotKey(Handle, 1);
            Restaurar();
            tray.Visible = false;
            base.OnFormClosed(e);
        }

        // ---------- Arquivos do cursor (embutidos no .exe) ----------
        void ExtrairArquivos()
        {
            try
            {
                Directory.CreateDirectory(pasta);
                Extrair("menino_animado.ani", arqAnimado);
                Extrair("menino_parado.cur", arqParado);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nao consegui preparar os arquivos do cursor:\n" + ex.Message, "AMJR Cursor Menino");
            }
        }

        void Extrair(string recurso, string destino)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(recurso))
            {
                if (s == null) throw new Exception("Recurso ausente: " + recurso);
                byte[] dados = new byte[s.Length];
                int lidos = 0;
                while (lidos < dados.Length) lidos += s.Read(dados, lidos, dados.Length - lidos);
                if (File.Exists(destino) && new FileInfo(destino).Length == dados.Length) return;
                File.WriteAllBytes(destino, dados);
            }
        }

        // ---------- Aplicar / restaurar ----------
        const string CHAVE_CURSORES = @"Control Panel\Cursors";
        string ultimoErro = "";

        void Aplicar()
        {
            ultimoErro = "";

            string arq = animado ? arqAnimado : arqParado;

            // Metodo 1 e 2: trocar direto na memoria (permite escolher o tamanho so do menino)
            bool ok = TrocarCursor(arq, OCR_NORMAL);
            if (!ok && animado) ok = TrocarCursor(arqParado, OCR_NORMAL);   // se o animado falhar, usa o parado
            if (ok && tambemLink) TrocarCursor(animado ? arqAnimado : arqParado, OCR_HAND);

            // Metodo 3: o mesmo caminho da tela "Propriedades do Mouse > Ponteiros"
            if (!ok) ok = AplicarViaRegistro(arq);

            if (!ok)
                tray.ShowBalloonTip(6000, "AMJR Cursor Menino",
                    "Nao foi possivel trocar o cursor.\n" + ultimoErro, ToolTipIcon.Warning);
        }

        bool TrocarCursor(string arq, uint id)
        {
            // Cada SetSystemCursor consome o handle, por isso carregamos de novo a cada troca
            IntPtr h = LoadImage(IntPtr.Zero, arq, IMAGE_CURSOR, tamanho, tamanho, LR_LOADFROMFILE);
            if (h == IntPtr.Zero)
            {
                ultimoErro += "LoadImage " + Path.GetFileName(arq) + " erro " + Marshal.GetLastWin32Error() + ". ";
                h = LoadCursorFromFile(arq);
            }
            if (h == IntPtr.Zero)
            {
                ultimoErro += "LoadCursorFromFile erro " + Marshal.GetLastWin32Error() + ". ";
                return false;
            }
            if (!SetSystemCursor(h, id))
            {
                ultimoErro += "SetSystemCursor erro " + Marshal.GetLastWin32Error() + ". ";
                return false;
            }
            return true;
        }

        bool AplicarViaRegistro(string arq)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CHAVE_CURSORES))
                {
                    // Guarda o que estava antes (uma vez so) para devolver depois
                    if (LerConfig("origArrow") == null)
                    {
                        GravarConfigExtra("origArrow", Convert.ToString(k.GetValue("Arrow", "")));
                        GravarConfigExtra("origHand", Convert.ToString(k.GetValue("Hand", "")));
                        object bs = k.GetValue("CursorBaseSize");
                        GravarConfigExtra("origBase", bs == null ? "" : bs.ToString());
                    }
                    k.SetValue("Arrow", arq);
                    if (tambemLink) k.SetValue("Hand", arq);
                    k.SetValue("CursorBaseSize", tamanho, Microsoft.Win32.RegistryValueKind.DWord);
                }

                bool ok = SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 3);
                if (!ok) ultimoErro += "Registro: SPI erro " + Marshal.GetLastWin32Error() + ". ";
                return ok;
            }
            catch (Exception ex) { ultimoErro += "Registro: " + ex.Message; return false; }
        }

        void Restaurar()
        {
            // Se mexemos no registro, devolve os valores originais
            string arrow = LerConfig("origArrow");
            if (arrow != null)
            {
                try
                {
                    using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CHAVE_CURSORES))
                    {
                        k.SetValue("Arrow", arrow);
                        k.SetValue("Hand", LerConfig("origHand") ?? "");
                        string bs = LerConfig("origBase");
                        int v;
                        if (!string.IsNullOrEmpty(bs) && int.TryParse(bs, out v))
                            k.SetValue("CursorBaseSize", v, Microsoft.Win32.RegistryValueKind.DWord);
                        else k.DeleteValue("CursorBaseSize", false);
                    }
                }
                catch { }
                ApagarConfigExtra();
            }

            // Recarrega o esquema de ponteiros que o usuario usa no Windows
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, arrow != null ? 3u : 0u);
        }

        // Valores extras guardados num arquivo separado (sobrevive a travamentos)
        string ArqExtra { get { return Path.Combine(pasta, "CursorMenino_original.ini"); } }

        string LerConfig(string chave)
        {
            try
            {
                if (!File.Exists(ArqExtra)) return null;
                foreach (string l in File.ReadAllLines(ArqExtra))
                {
                    int i = l.IndexOf('=');
                    if (i > 0 && l.Substring(0, i) == chave) return l.Substring(i + 1);
                }
            }
            catch { }
            return null;
        }

        void GravarConfigExtra(string chave, string valor)
        {
            Directory.CreateDirectory(pasta);
            File.AppendAllText(ArqExtra, chave + "=" + valor + Environment.NewLine);
        }

        void ApagarConfigExtra()
        {
            try { File.Delete(ArqExtra); } catch { }
        }

        void Reaplicar()
        {
            SalvarConfig();
            if (!ligado) { AtualizarMenu(); return; }
            Restaurar();
            Aplicar();
            AtualizarMenu();
        }

        void Alternar()
        {
            ligado = !ligado;
            if (ligado) Aplicar(); else Restaurar();
            SalvarConfig();
            AtualizarMenu();
        }

        // ---------- Bandeja ----------
        ToolStripMenuItem itemLigar, itemAnimado, itemParado, itemLink, itemIniciar, menuTam;

        void MontarBandeja()
        {
            try { tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { tray.Icon = SystemIcons.Application; }

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem titulo = new ToolStripMenuItem("AMJR Cursor Menino  -  Prof. Alvaro");
            titulo.Enabled = false;
            menu.Items.Add(titulo);
            menu.Items.Add(new ToolStripSeparator());

            itemLigar = new ToolStripMenuItem("Ligado   (Ctrl+Alt+M)", null, delegate { Alternar(); });
            menu.Items.Add(itemLigar);

            itemAnimado = new ToolStripMenuItem("Animado (luz e brilhos)", null, delegate { animado = true; Reaplicar(); });
            itemParado = new ToolStripMenuItem("Parado", null, delegate { animado = false; Reaplicar(); });
            menu.Items.Add(itemAnimado);
            menu.Items.Add(itemParado);

            menuTam = new ToolStripMenuItem("Tamanho");
            AddTam("Normal", 48); AddTam("Grande", 64); AddTam("Muito grande", 96); AddTam("Gigante", 128);
            menu.Items.Add(menuTam);

            itemLink = new ToolStripMenuItem("Usar tambem nos links (maozinha)", null, delegate { tambemLink = !tambemLink; Reaplicar(); });
            menu.Items.Add(itemLink);

            itemIniciar = new ToolStripMenuItem("Iniciar com o Windows", null, delegate { DefinirInicio(!IniciaComWindows()); AtualizarMenu(); });
            menu.Items.Add(itemIniciar);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Sair (volta o cursor normal)", null, delegate { Close(); }));

            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Alternar(); };
            tray.Visible = true;
            AtualizarMenu();
        }

        void AddTam(string nome, int px)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(nome, null, delegate { tamanho = px; Reaplicar(); });
            it.Tag = px;
            menuTam.DropDownItems.Add(it);
        }

        void AtualizarMenu()
        {
            itemLigar.Checked = ligado;
            itemAnimado.Checked = animado;
            itemParado.Checked = !animado;
            itemLink.Checked = tambemLink;
            itemIniciar.Checked = IniciaComWindows();
            foreach (ToolStripMenuItem it in menuTam.DropDownItems) it.Checked = (int)it.Tag == tamanho;
            tray.Text = ligado ? "AMJR Cursor Menino - ligado (Ctrl+Alt+M)" : "AMJR Cursor Menino - desligado (Ctrl+Alt+M)";
        }

        // ---------- Iniciar com o Windows ----------
        const string CHAVE_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";

        bool IniciaComWindows()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CHAVE_RUN))
                    return k != null && k.GetValue("AMJR Cursor Menino") != null;
            }
            catch { return false; }
        }

        void DefinirInicio(bool ativar)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CHAVE_RUN))
                {
                    if (ativar) k.SetValue("AMJR Cursor Menino", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("AMJR Cursor Menino", false);
                }
            }
            catch { }
        }

        // ---------- Configuracao ----------
        void CarregarConfig()
        {
            try
            {
                if (!File.Exists(arqConfig)) return;
                foreach (string linha in File.ReadAllLines(arqConfig))
                {
                    string[] kv = linha.Split('=');
                    if (kv.Length != 2) continue;
                    int v;
                    if (!int.TryParse(kv[1].Trim(), out v)) continue;
                    switch (kv[0].Trim())
                    {
                        case "animado": animado = v != 0; break;
                        case "tamanho": tamanho = v; break;
                        case "link": tambemLink = v != 0; break;
                    }
                }
            }
            catch { }
        }

        void SalvarConfig()
        {
            try
            {
                Directory.CreateDirectory(pasta);
                File.WriteAllLines(arqConfig, new string[] {
                    "animado=" + (animado ? 1 : 0),
                    "tamanho=" + tamanho,
                    "link=" + (tambemLink ? 1 : 0)
                });
            }
            catch { }
        }

        // ---------- Inicio ----------
        [STAThread]
        static void Main()
        {
            bool novo;
            using (System.Threading.Mutex mtx = new System.Threading.Mutex(true, "AMJR_CursorMenino_Unico", out novo))
            {
                if (!novo)
                {
                    MessageBox.Show("O AMJR Cursor Menino ja esta aberto.\nProcure o icone perto do relogio.",
                        "AMJR Cursor Menino", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.Run(new CursorMenino());
            }
        }
    }
}
