using System.Globalization;
using System.Text;
using static VirtualMemoryLab.NativeMethods;

namespace VirtualMemoryLab;

public class MainForm : Form
{
    // Строки информации о системе
    const string SProc      = "Processor Architecture        {0}";
    const string SPageSize  = "Page Size                     0x{0:X8} bytes ({1} КБ)";
    const string SMinAddr   = "Minimum Application Address   {0}";
    const string SMaxAddr   = "Maximum Application Address   {0}";
    const string SNumProcs  = "Number of Processors          {0}";
    const string SProcMask  = "Active Processor Mask         0x{0:X}";
    const string SProcType  = "Processor Type                {0}";
    const string SAllocGra  = "Allocation Granularity        0x{0:X8} bytes ({1} КБ)";
    const string SProcLevel = "Processor Level               {0}";
    const string SProcRev   = "Processor Revision            {0:X4}";
    const string SIntel3 = "80386";
    const string SIntel4 = "80486";
    const string SIntel5 = "Pentium";
    const string SIntel6 = "Pentium Pro и новее (семейство 6)";

    // Строки состояния памяти
    const string SMemUse   = "Memory in use                 {0}%";
    const string STotMem   = "Total physical memory         0x{0:X16} bytes ({1})";
    const string SFreeMem  = "Free physical memory          0x{0:X16} bytes ({1})";
    const string STotPage  = "Total page file memory        0x{0:X16} bytes ({1})";
    const string SFreePage = "Free page file memory         0x{0:X16} bytes ({1})";
    const string STotVirt  = "Total virtual memory          0x{0:X16} bytes ({1})";
    const string SFreeVirt = "Free virtual memory           0x{0:X16} bytes ({1})";

    readonly Font monoFont = new("Consolas", 10f);

    // Вкладка 6.2.1
    TextBox memoSysInfo;
    // Вкладка 6.2.2
    TextBox memoMemStatus;
    CheckBox chkAutoRefresh;
    System.Windows.Forms.Timer refreshTimer;
    // Вкладка 6.2.3
    TextBox txtAddress;
    TextBox memoQuery;
    ListView lvMap;
    Label lblMapSummary;
    TextBox txtAllocSize;
    ComboBox cbProtect;
    Label lblAllocState;

    SYSTEM_INFO si;
    IntPtr allocatedRegion = IntPtr.Zero;

    public MainForm()
    {
        Text = "Исследование виртуальной памяти ОС Windows";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        GetSystemInfo(out si);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateSysInfoPage());
        tabs.TabPages.Add(CreateMemStatusPage());
        tabs.TabPages.Add(CreateVirtualQueryPage());
        Controls.Add(tabs);

        FormClosing += (_, _) =>
        {
            if (allocatedRegion != IntPtr.Zero)
                VirtualFree(allocatedRegion, UIntPtr.Zero, MEM_RELEASE);
        };
    }

    // ===================== 6.2.1. GetSystemInfo =====================

    TabPage CreateSysInfoPage()
    {
        var page = new TabPage("6.2.1  GetSystemInfo");
        memoSysInfo = CreateMemo();
        var btn = new Button { Text = "Получить информацию о системе", AutoSize = true };
        var btnClear = new Button { Text = "Очистить", AutoSize = true };
        btn.Click += F1Click;
        btnClear.Click += (_, _) => memoSysInfo.Clear();
        page.Controls.Add(memoSysInfo);
        page.Controls.Add(CreateButtonPanel(btn, btnClear));
        return page;
    }

    /// <summary>Получение системной информации функцией GetSystemInfo.</summary>
    void F1Click(object sender, EventArgs e)
    {
        GetSystemInfo(out si);

        AddLine(memoSysInfo, "===== GetSystemInfo  " + DateTime.Now.ToString("HH:mm:ss") + " =====");
        AddLine(memoSysInfo, string.Format(SProc, ArchitectureName(si.wProcessorArchitecture)));
        AddLine(memoSysInfo, string.Format(SPageSize, si.dwPageSize, si.dwPageSize / 1024));
        AddLine(memoSysInfo, string.Format(SMinAddr, Ptr(si.lpMinimumApplicationAddress)));
        AddLine(memoSysInfo, string.Format(SMaxAddr, Ptr(si.lpMaximumApplicationAddress)));
        AddLine(memoSysInfo, string.Format(SNumProcs, si.dwNumberOfProcessors));
        AddLine(memoSysInfo, string.Format(SProcMask, (ulong)si.dwActiveProcessorMask));
        AddLine(memoSysInfo, string.Format(SProcType, si.dwProcessorType));
        AddLine(memoSysInfo, string.Format(SAllocGra, si.dwAllocationGranularity, si.dwAllocationGranularity / 1024));
        string level = si.wProcessorLevel switch
        {
            3 => SIntel3,
            4 => SIntel4,
            5 => SIntel5,
            6 => SIntel6,
            _ => si.wProcessorLevel.ToString()
        };
        AddLine(memoSysInfo, string.Format(SProcLevel, level));
        AddLine(memoSysInfo, string.Format(SProcRev, si.wProcessorRevision) +
            $"   (модель {si.wProcessorRevision >> 8}, степпинг {si.wProcessorRevision & 0xFF})");

        ulong userSpace = (ulong)si.lpMaximumApplicationAddress - (ulong)si.lpMinimumApplicationAddress + 1;
        AddLine(memoSysInfo, "");
        AddLine(memoSysInfo, "Пояснения:");
        AddLine(memoSysInfo, $"  Разрядность процесса: {IntPtr.Size * 8} бит");
        AddLine(memoSysInfo, $"  Пользовательская часть адресного пространства: {SizeText(userSpace)}");
        AddLine(memoSysInfo, $"  Регионы выравниваются по {si.dwAllocationGranularity / 1024} КБ, " +
                             $"размер региона кратен странице {si.dwPageSize / 1024} КБ");
        AddLine(memoSysInfo, "");
    }

    static string ArchitectureName(ushort arch) => arch switch
    {
        PROCESSOR_ARCHITECTURE_INTEL => "Intel (x86)",
        PROCESSOR_ARCHITECTURE_MIPS => "MIPS",
        PROCESSOR_ARCHITECTURE_ALPHA => "Alpha",
        PROCESSOR_ARCHITECTURE_PPC => "PowerPC",
        PROCESSOR_ARCHITECTURE_ARM => "ARM",
        PROCESSOR_ARCHITECTURE_IA64 => "Intel Itanium (IA-64)",
        PROCESSOR_ARCHITECTURE_AMD64 => "x64 (AMD64 / Intel 64)",
        PROCESSOR_ARCHITECTURE_ARM64 => "ARM64",
        _ => "Неизвестная (" + arch + ")"
    };

    // ===================== 6.2.2. GlobalMemoryStatus =====================

    TabPage CreateMemStatusPage()
    {
        var page = new TabPage("6.2.2  GlobalMemoryStatus");
        memoMemStatus = CreateMemo();
        var btn = new Button { Text = "Состояние памяти", AutoSize = true };
        var btnClear = new Button { Text = "Очистить", AutoSize = true };
        chkAutoRefresh = new CheckBox { Text = "Отслеживать каждые 2 с", AutoSize = true, Margin = new Padding(12, 8, 3, 3) };
        refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        refreshTimer.Tick += F2Click;
        chkAutoRefresh.CheckedChanged += (_, _) => refreshTimer.Enabled = chkAutoRefresh.Checked;
        btn.Click += F2Click;
        btnClear.Click += (_, _) => memoMemStatus.Clear();
        page.Controls.Add(memoMemStatus);
        page.Controls.Add(CreateButtonPanel(btn, btnClear, chkAutoRefresh));
        return page;
    }

    /// <summary>Определение текущего состояния памяти функцией GlobalMemoryStatus.</summary>
    void F2Click(object sender, EventArgs e)
    {
        var ms = new MEMORYSTATUS();
        ms.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUS>();
        GlobalMemoryStatus(ref ms);

        AddLine(memoMemStatus, "===== GlobalMemoryStatus  " + DateTime.Now.ToString("HH:mm:ss") + " =====");
        AddLine(memoMemStatus, string.Format(SMemUse, ms.dwMemoryLoad));
        AddLine(memoMemStatus, string.Format(STotMem, (ulong)ms.dwTotalPhys, SizeText((ulong)ms.dwTotalPhys)));
        AddLine(memoMemStatus, string.Format(SFreeMem, (ulong)ms.dwAvailPhys, SizeText((ulong)ms.dwAvailPhys)));
        AddLine(memoMemStatus, string.Format(STotPage, (ulong)ms.dwTotalPageFile, SizeText((ulong)ms.dwTotalPageFile)));
        AddLine(memoMemStatus, string.Format(SFreePage, (ulong)ms.dwAvailPageFile, SizeText((ulong)ms.dwAvailPageFile)));
        AddLine(memoMemStatus, string.Format(STotVirt, (ulong)ms.dwTotalVirtual, SizeText((ulong)ms.dwTotalVirtual)));
        AddLine(memoMemStatus, string.Format(SFreeVirt, (ulong)ms.dwAvailVirtual, SizeText((ulong)ms.dwAvailVirtual)));

        ulong usedPhys = (ulong)ms.dwTotalPhys - (ulong)ms.dwAvailPhys;
        ulong usedVirt = (ulong)ms.dwTotalVirtual - (ulong)ms.dwAvailVirtual;
        AddLine(memoMemStatus, $"  -> занято физической памяти: {SizeText(usedPhys)}");
        AddLine(memoMemStatus, $"  -> занято адресного пространства этим процессом: {SizeText(usedVirt)}");
        AddLine(memoMemStatus, "");
    }

    // ===================== 6.2.3. VirtualQuery =====================

    TabPage CreateVirtualQueryPage()
    {
        var page = new TabPage("6.2.3  VirtualQuery");

        // --- Верхняя панель: адрес и кнопки запроса ---
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        txtAddress = new TextBox { Width = 180, Font = monoFont, Text = Ptr(si.lpMinimumApplicationAddress) };
        var btnMin = new Button { Text = "Мин. адрес (6.2.1)", AutoSize = true };
        var btnMax = new Button { Text = "Макс. адрес (6.2.1)", AutoSize = true };
        var btnQuery = new Button { Text = "VirtualQuery", AutoSize = true };
        var btnNext = new Button { Text = "Следующий регион →", AutoSize = true };
        var btnMap = new Button { Text = "Карта адресного пространства", AutoSize = true };
        btnMin.Click += (_, _) => { txtAddress.Text = Ptr(si.lpMinimumApplicationAddress); QueryAddress(); };
        btnMax.Click += (_, _) => { txtAddress.Text = Ptr(si.lpMaximumApplicationAddress); QueryAddress(); };
        btnQuery.Click += (_, _) => QueryAddress();
        btnNext.Click += (_, _) => QueryNextRegion();
        btnMap.Click += (_, _) => BuildMemoryMap();
        txtAddress.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { QueryAddress(); e.SuppressKeyPress = true; } };
        top.Controls.AddRange(new Control[]
        {
            new Label { Text = "Адрес lpAddress:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) },
            txtAddress, btnQuery, btnNext, btnMin, btnMax, btnMap
        });

        // --- Панель VirtualAlloc / VirtualFree ---
        var allocPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        txtAllocSize = new TextBox { Width = 70, Text = "256" };
        cbProtect = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        cbProtect.Items.AddRange(new object[]
        {
            "PAGE_NOACCESS", "PAGE_READONLY", "PAGE_READWRITE", "PAGE_EXECUTE",
            "PAGE_EXECUTE_READ", "PAGE_EXECUTE_READWRITE"
        });
        cbProtect.SelectedIndex = 2;
        var btnReserve = new Button { Text = "MEM_RESERVE", AutoSize = true };
        var btnCommit = new Button { Text = "MEM_COMMIT", AutoSize = true };
        var btnDecommit = new Button { Text = "MEM_DECOMMIT", AutoSize = true };
        var btnRelease = new Button { Text = "MEM_RELEASE", AutoSize = true };
        lblAllocState = new Label { AutoSize = true, Margin = new Padding(10, 8, 3, 3), Text = "Регион не выделен" };
        btnReserve.Click += (_, _) => ReserveRegion();
        btnCommit.Click += (_, _) => CommitRegion();
        btnDecommit.Click += (_, _) => DecommitRegion();
        btnRelease.Click += (_, _) => ReleaseRegion();
        allocPanel.Controls.AddRange(new Control[]
        {
            new Label { Text = "VirtualAlloc: размер, КБ", AutoSize = true, Margin = new Padding(3, 8, 3, 3) },
            txtAllocSize,
            new Label { Text = "защита:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) },
            cbProtect, btnReserve, btnCommit,
            new Label { Text = "VirtualFree:", AutoSize = true, Margin = new Padding(10, 8, 3, 3) },
            btnDecommit, btnRelease, lblAllocState
        });

        // --- Результат запроса и карта памяти ---
        memoQuery = CreateMemo();
        lvMap = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Consolas", 9f)
        };
        lvMap.Columns.Add("BaseAddress", 150);
        lvMap.Columns.Add("AllocationBase", 150);
        lvMap.Columns.Add("RegionSize", 120, HorizontalAlignment.Right);
        lvMap.Columns.Add("State", 90);
        lvMap.Columns.Add("Protect", 190);
        lvMap.Columns.Add("AllocationProtect", 190);
        lvMap.Columns.Add("Type", 90);
        lvMap.Columns.Add("Описание", 400);
        lvMap.DoubleClick += (_, _) =>
        {
            if (lvMap.SelectedItems.Count == 0) return;
            txtAddress.Text = lvMap.SelectedItems[0].Text;
            QueryAddress();
        };
        lblMapSummary = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 22, Padding = new Padding(4, 3, 0, 0) };

        var mapPanel = new Panel { Dock = DockStyle.Fill };
        mapPanel.Controls.Add(lvMap);
        mapPanel.Controls.Add(lblMapSummary);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(memoQuery);
        split.Panel2.Controls.Add(mapPanel);

        page.Controls.Add(split);
        page.Controls.Add(allocPanel);
        page.Controls.Add(top);
        page.Layout += (_, _) => { if (split.Height > 300) split.SplitterDistance = 270; };
        return page;
    }

    /// <summary>Запрос информации об участке памяти по адресу из поля ввода.</summary>
    void QueryAddress()
    {
        if (!TryParseAddress(txtAddress.Text, out IntPtr addr))
        {
            MessageBox.Show("Введите адрес в шестнадцатеричном виде, например 0x7FFE0000",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        QueryAndShow(addr);
    }

    void QueryNextRegion()
    {
        if (!TryParseAddress(txtAddress.Text, out IntPtr addr)) return;
        if (!Query(addr, out var mbi)) return;
        ulong next = (ulong)mbi.BaseAddress + (ulong)mbi.RegionSize;
        if (next > (ulong)si.lpMaximumApplicationAddress)
        {
            AddLine(memoQuery, "Достигнут максимальный адрес приложения.");
            return;
        }
        txtAddress.Text = Ptr((IntPtr)(long)next);
        QueryAndShow((IntPtr)(long)next);
    }

    bool Query(IntPtr addr, out MEMORY_BASIC_INFORMATION mbi)
    {
        var size = (UIntPtr)System.Runtime.InteropServices.Marshal.SizeOf<MEMORY_BASIC_INFORMATION>();
        UIntPtr result = VirtualQuery(addr, out mbi, size);
        return result != UIntPtr.Zero;
    }

    void QueryAndShow(IntPtr addr)
    {
        memoQuery.Clear();
        if (!Query(addr, out var mbi))
        {
            int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            AddLine(memoQuery, $"VirtualQuery({Ptr(addr)}) вернула 0. Код ошибки {err}: " +
                               new System.ComponentModel.Win32Exception(err).Message);
            AddLine(memoQuery, $"Допустимый диапазон: {Ptr(si.lpMinimumApplicationAddress)} .. {Ptr(si.lpMaximumApplicationAddress)}");
            return;
        }

        AddLine(memoQuery, $"VirtualQuery(lpAddress = {Ptr(addr)})  -> структура MEMORY_BASIC_INFORMATION:");
        AddLine(memoQuery, $"  BaseAddress       = {Ptr(mbi.BaseAddress)}   (адрес, округлённый вниз до границы страницы)");
        if (mbi.State == MEM_FREE)
        {
            AddLine(memoQuery, "  AllocationBase    = не определено (MEM_FREE)");
            AddLine(memoQuery, "  AllocationProtect = не определено (MEM_FREE)");
        }
        else
        {
            AddLine(memoQuery, $"  AllocationBase    = {Ptr(mbi.AllocationBase)}   (базовый адрес региона)");
            AddLine(memoQuery, $"  AllocationProtect = 0x{mbi.AllocationProtect:X8}   {ProtectText(mbi.AllocationProtect)}  (защита при резервировании)");
        }
        AddLine(memoQuery, $"  RegionSize        = 0x{(ulong)mbi.RegionSize:X} байт = {SizeText((ulong)mbi.RegionSize)}, " +
                           $"{(ulong)mbi.RegionSize / si.dwPageSize} стр.");
        AddLine(memoQuery, $"  State             = 0x{mbi.State:X8}   {StateText(mbi.State)} — {StateDescription(mbi.State)}");
        if (mbi.State == MEM_FREE)
        {
            AddLine(memoQuery, "  Protect           = не определено (MEM_FREE)");
            AddLine(memoQuery, "  Type              = не определено (MEM_FREE)");
        }
        else
        {
            AddLine(memoQuery, $"  Protect           = 0x{mbi.Protect:X8}   {ProtectText(mbi.Protect)}  (текущая защита)");
            AddLine(memoQuery, $"  Type              = 0x{mbi.Type:X8}   {TypeText(mbi.Type)} — {TypeDescription(mbi.Type)}");
        }
        string owner = DescribeRegion(mbi);
        if (owner.Length > 0)
            AddLine(memoQuery, $"  Содержимое        : {owner}");
        ulong end = (ulong)mbi.BaseAddress + (ulong)mbi.RegionSize - 1;
        AddLine(memoQuery, $"  Диапазон страниц  : {Ptr(mbi.BaseAddress)} .. {Ptr((IntPtr)(long)end)}");
    }

    /// <summary>Обход всего пользовательского адресного пространства функцией VirtualQuery.</summary>
    void BuildMemoryMap()
    {
        Cursor = Cursors.WaitCursor;
        lvMap.BeginUpdate();
        lvMap.Items.Clear();

        ulong free = 0, reserved = 0, committed = 0;
        int count = 0;
        ulong addr = (ulong)si.lpMinimumApplicationAddress;
        ulong max = (ulong)si.lpMaximumApplicationAddress;

        while (addr <= max && Query((IntPtr)(long)addr, out var mbi))
        {
            ulong size = (ulong)mbi.RegionSize;
            switch (mbi.State)
            {
                case MEM_FREE: free += size; break;
                case MEM_RESERVE: reserved += size; break;
                case MEM_COMMIT: committed += size; break;
            }

            bool isFree = mbi.State == MEM_FREE;
            var item = new ListViewItem(Ptr(mbi.BaseAddress));
            item.SubItems.Add(isFree ? "" : Ptr(mbi.AllocationBase));
            item.SubItems.Add(SizeText(size));
            item.SubItems.Add(StateText(mbi.State));
            item.SubItems.Add(isFree || mbi.State == MEM_RESERVE ? "" : ProtectText(mbi.Protect));
            item.SubItems.Add(isFree ? "" : ProtectText(mbi.AllocationProtect));
            item.SubItems.Add(isFree ? "" : TypeText(mbi.Type));
            item.SubItems.Add(DescribeRegion(mbi));
            item.ForeColor = mbi.State switch
            {
                MEM_FREE => Color.Gray,
                MEM_RESERVE => Color.DarkGoldenrod,
                _ => mbi.Type == MEM_IMAGE ? Color.DarkBlue : mbi.Type == MEM_MAPPED ? Color.DarkGreen : Color.Black
            };
            if (allocatedRegion != IntPtr.Zero && mbi.AllocationBase == allocatedRegion && !isFree)
                item.BackColor = Color.LightYellow;
            lvMap.Items.Add(item);
            count++;

            ulong next = (ulong)mbi.BaseAddress + size;
            if (next <= addr) break;
            addr = next;
        }

        lvMap.EndUpdate();
        Cursor = Cursors.Default;
        lblMapSummary.Text = $"Блоков: {count}   MEM_COMMIT: {SizeText(committed)}   " +
                             $"MEM_RESERVE: {SizeText(reserved)}   MEM_FREE: {SizeText(free)}   " +
                             "(двойной щелчок по строке — подробности VirtualQuery)";
    }

    // ----- VirtualAlloc / VirtualFree -----

    uint SelectedProtect() => cbProtect.SelectedIndex switch
    {
        0 => PAGE_NOACCESS,
        1 => PAGE_READONLY,
        2 => PAGE_READWRITE,
        3 => PAGE_EXECUTE,
        4 => PAGE_EXECUTE_READ,
        _ => PAGE_EXECUTE_READWRITE
    };

    bool TryGetAllocSize(out ulong bytes)
    {
        bytes = 0;
        if (!ulong.TryParse(txtAllocSize.Text, out ulong kb) || kb == 0)
        {
            MessageBox.Show("Размер задаётся целым числом КБ больше нуля", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        bytes = kb * 1024;
        return true;
    }

    void ReserveRegion()
    {
        if (allocatedRegion != IntPtr.Zero)
        {
            MessageBox.Show("Регион уже зарезервирован. Сначала освободите его (MEM_RELEASE).", Text);
            return;
        }
        if (!TryGetAllocSize(out ulong bytes)) return;

        allocatedRegion = VirtualAlloc(IntPtr.Zero, (UIntPtr)bytes, MEM_RESERVE, SelectedProtect());
        if (allocatedRegion == IntPtr.Zero)
        {
            ShowWin32Error("VirtualAlloc(MEM_RESERVE)");
            return;
        }
        lblAllocState.Text = $"Зарезервирован регион {Ptr(allocatedRegion)}, {SizeText(bytes)}";
        txtAddress.Text = Ptr(allocatedRegion);
        QueryAndShow(allocatedRegion);
        AddLine(memoQuery, "");
        AddLine(memoQuery, $"Адрес региона кратен гранулярности 0x{si.dwAllocationGranularity:X}: " +
                           $"{(ulong)allocatedRegion % si.dwAllocationGranularity == 0}");
    }

    void CommitRegion()
    {
        if (allocatedRegion == IntPtr.Zero)
        {
            MessageBox.Show("Сначала зарезервируйте регион (MEM_RESERVE).", Text);
            return;
        }
        if (!TryGetAllocSize(out ulong bytes)) return;

        IntPtr p = VirtualAlloc(allocatedRegion, (UIntPtr)bytes, MEM_COMMIT, SelectedProtect());
        if (p == IntPtr.Zero)
        {
            ShowWin32Error("VirtualAlloc(MEM_COMMIT)");
            return;
        }
        lblAllocState.Text = $"Регион {Ptr(allocatedRegion)}: передано {SizeText(bytes)} физической памяти";
        txtAddress.Text = Ptr(allocatedRegion);
        QueryAndShow(allocatedRegion);
    }

    void DecommitRegion()
    {
        if (allocatedRegion == IntPtr.Zero) return;
        if (!TryGetAllocSize(out ulong bytes)) return;

        if (!VirtualFree(allocatedRegion, (UIntPtr)bytes, MEM_DECOMMIT))
        {
            ShowWin32Error("VirtualFree(MEM_DECOMMIT)");
            return;
        }
        lblAllocState.Text = $"Регион {Ptr(allocatedRegion)}: физическая память возвращена, регион остаётся зарезервированным";
        QueryAndShow(allocatedRegion);
    }

    void ReleaseRegion()
    {
        if (allocatedRegion == IntPtr.Zero) return;
        IntPtr old = allocatedRegion;
        // При MEM_RELEASE размер обязан быть равен нулю — система знает размер региона
        if (!VirtualFree(allocatedRegion, UIntPtr.Zero, MEM_RELEASE))
        {
            ShowWin32Error("VirtualFree(MEM_RELEASE)");
            return;
        }
        allocatedRegion = IntPtr.Zero;
        lblAllocState.Text = "Регион освобождён";
        txtAddress.Text = Ptr(old);
        QueryAndShow(old);
    }

    void ShowWin32Error(string func)
    {
        int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        MessageBox.Show($"{func}: ошибка {err} — {new System.ComponentModel.Win32Exception(err).Message}",
            Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    // ===================== Расшифровка значений =====================

    static string StateText(uint state) => state switch
    {
        MEM_COMMIT => "MEM_COMMIT",
        MEM_RESERVE => "MEM_RESERVE",
        MEM_FREE => "MEM_FREE",
        _ => "0x" + state.ToString("X")
    };

    static string StateDescription(uint state) => state switch
    {
        MEM_COMMIT => "страницам передана физическая память",
        MEM_RESERVE => "регион зарезервирован, физическая память не передана",
        MEM_FREE => "свободный участок адресного пространства",
        _ => "неизвестно"
    };

    static string TypeText(uint type) => type switch
    {
        MEM_IMAGE => "MEM_IMAGE",
        MEM_MAPPED => "MEM_MAPPED",
        MEM_PRIVATE => "MEM_PRIVATE",
        0 => "",
        _ => "0x" + type.ToString("X")
    };

    static string TypeDescription(uint type) => type switch
    {
        MEM_IMAGE => "спроецирован образ исполняемого модуля (EXE/DLL)",
        MEM_MAPPED => "спроецирован файл (memory-mapped file)",
        MEM_PRIVATE => "закрытая память процесса (VirtualAlloc, куча, стек)",
        _ => "неизвестно"
    };

    static string ProtectText(uint protect)
    {
        if (protect == 0) return "";
        string basic = (protect & 0xFF) switch
        {
            PAGE_NOACCESS => "PAGE_NOACCESS",
            PAGE_READONLY => "PAGE_READONLY",
            PAGE_READWRITE => "PAGE_READWRITE",
            PAGE_WRITECOPY => "PAGE_WRITECOPY",
            PAGE_EXECUTE => "PAGE_EXECUTE",
            PAGE_EXECUTE_READ => "PAGE_EXECUTE_READ",
            PAGE_EXECUTE_READWRITE => "PAGE_EXECUTE_READWRITE",
            PAGE_EXECUTE_WRITECOPY => "PAGE_EXECUTE_WRITECOPY",
            _ => "0x" + (protect & 0xFF).ToString("X")
        };
        if ((protect & PAGE_GUARD) != 0) basic += " | PAGE_GUARD";
        if ((protect & PAGE_NOCACHE) != 0) basic += " | PAGE_NOCACHE";
        if ((protect & PAGE_WRITECOMBINE) != 0) basic += " | PAGE_WRITECOMBINE";
        return basic;
    }

    string DescribeRegion(MEMORY_BASIC_INFORMATION mbi)
    {
        if (mbi.State == MEM_FREE) return "";
        if (mbi.Type == MEM_IMAGE || mbi.Type == MEM_MAPPED)
        {
            var sb = new StringBuilder(1024);
            if (GetMappedFileName(GetCurrentProcess(), mbi.BaseAddress, sb, (uint)sb.Capacity) > 0)
                return Path.GetFileName(sb.ToString());
        }
        if (allocatedRegion != IntPtr.Zero && mbi.AllocationBase == allocatedRegion)
            return "<- регион, выделенный кнопками VirtualAlloc";
        if ((mbi.Protect & PAGE_GUARD) != 0)
            return "сторожевая страница (стек потока)";
        return "";
    }

    // ===================== Вспомогательные функции =====================

    TextBox CreateMemo() => new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        ReadOnly = true,
        BackColor = SystemColors.Window,
        Dock = DockStyle.Fill,
        Font = monoFont
    };

    static FlowLayoutPanel CreateButtonPanel(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        panel.Controls.AddRange(controls);
        return panel;
    }

    static void AddLine(TextBox memo, string line) => memo.AppendText(line + Environment.NewLine);

    static string Ptr(IntPtr p) => "0x" + ((ulong)(long)p).ToString(IntPtr.Size == 8 ? "X16" : "X8");

    static bool TryParseAddress(string text, out IntPtr addr)
    {
        text = text.Trim().Replace("_", "").Replace("`", "");
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        else if (text.StartsWith("$")) text = text[1..];
        bool ok = ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value);
        addr = (IntPtr)(long)value;
        return ok;
    }

    static string SizeText(ulong bytes)
    {
        string[] units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{bytes} Б" : $"{v:0.##} {units[i]}";
    }
}
