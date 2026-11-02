using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Quina.Core;
using Quina.Infrastructure;

namespace Quina.Desktop;

/// <summary>Apenas apresentação e coordenação de telas. Regras, rede e bot ficam nos serviços.</summary>
public partial class MainWindow : Window
{
    private DominoApiClient api = new();
    private IGameGateway gateway;
    private readonly WindowsVault accountVault = new();
    private WindowsVault? playerVault;
    private IIntentStore intentStore = new MemoryIntentStore();
    private GameController? controller;
    private readonly IBotStrategy bot = new GreedyBot();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Stopwatch turnClock = new();
    private readonly CancellationTokenSource lifetime = new();
    private (Guid Match, int Round, Guid? Player)? lastTurn;
    private ActionMap map = new();
    private Tile? selected;
    private GameState? renderedState;
    private DemoSnapshot? savedCpu;
    private bool saveFailed;
    private Guid player;
    private bool busy, ready, mapConfirmed;
    private bool accountAuthenticated;
    private DateTimeOffset nextPoll;
    private int failures;
    private bool fullscreen;
    private WindowState stateBeforeFullscreen;
    private WindowStyle styleBeforeFullscreen;
    private ResizeMode resizeBeforeFullscreen;

    public MainWindow()
    {
        InitializeComponent();
        gateway = api;
        foreach (var tile in new[] { new Tile(6, 6), new Tile(6, 4), new Tile(4, 1) })
            LobbyTiles.Children.Add(new TileView(tile) { Margin = new Thickness(3), RenderTransform = new RotateTransform(tile.ValorB == 6 ? -12 : tile.ValorB == 1 ? 12 : 0), RenderTransformOrigin = new Point(.5, .5) });
        foreach (var tile in new[] { new Tile(6, 3), new Tile(3, 5), new Tile(5, 5) })
            WelcomeTiles.Children.Add(new TileView(tile) { Margin = new Thickness(5), RenderTransform = new RotateTransform(-8), RenderTransformOrigin = new Point(.5, .5) });
        timer.Tick += Tick;
        timer.Start();
        Closing += OnClosing;
        StateChanged += (_, _) => UpdateWindowButtons();
        UpdateWindowButtons();
        try { savedCpu = accountVault.Get<DemoSnapshot>("cpuGame"); }
        catch (Exception) { Notify("Não foi possível ler a partida local salva. Você ainda pode iniciar uma nova.", true); }
        UpdateButtons();
    }

    private void UpdateWindowButtons()
    {
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "Restaurar janela" : "Maximizar";
        MaximizeButton.IsEnabled = !fullscreen;
        FullscreenButton.Content = fullscreen ? "Sair da tela cheia (F11)" : "Tela cheia (F11)";
    }
    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            if (!e.IsRepeat) ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && fullscreen)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }
    private void ToggleFullscreen()
    {
        if (!fullscreen)
        {
            stateBeforeFullscreen = WindowState;
            styleBeforeFullscreen = WindowStyle;
            resizeBeforeFullscreen = ResizeMode;
            fullscreen = true;
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            fullscreen = false;
            WindowState = WindowState.Normal;
            WindowStyle = styleBeforeFullscreen;
            ResizeMode = resizeBeforeFullscreen;
            WindowState = stateBeforeFullscreen;
        }
        UpdateWindowButtons();
    }

    private void Notify(string message, bool error = false)
    {
        AccessNotice.Text = message;
        AccessNotice.Foreground = new SolidColorBrush(error ? Color.FromRgb(239, 157, 151) : Color.FromRgb(171, 193, 185));
        AccessNotice.Visibility = Visibility.Visible;
        NoticeText.Text = message;
        NoticeText.Foreground = new SolidColorBrush(error ? Color.FromRgb(239, 157, 151) : Color.FromRgb(171, 193, 185));
        LogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        var lines = LogBox.Text.Split(Environment.NewLine);
        if (lines.Length > 180) LogBox.Text = string.Join(Environment.NewLine, lines.Skip(lines.Length - 160));
        LogBox.ScrollToEnd();
    }

    private async Task RunAsync(Func<Task> operation, string? errorTitle = null)
    {
        if (busy) { Notify("Aguarde a operação atual."); return; }
        busy = true; BusyText.Text = "Conectando…"; UpdateButtons();
        try { await operation(); failures = 0; }
        catch (Exception ex)
        {
            AutoBox.IsChecked = false;
            failures++;
            var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(5, failures))));
            if (ex is GameApiException error)
            {
                if (error.RetryAfter is { } retryAfter && retryAfter > delay) delay = retryAfter;
                if (error.Status is 401 or 403) { accountAuthenticated = false; ready = false; Tabs.SelectedItem = AccessTab; ShowAccessStep(1); }
                if (error.Uncertain && controller?.Pending is null)
                    Notify(error.Message + " Consulte perfis/mesas antes de repetir uma criação.", true);
                else Notify(error.Message, true);
            }
            else Notify(ex.Message, true);
            if (errorTitle is not null)
            {
                CreateMatchNotice.Text = NoticeText.Text;
                CreateMatchNotice.Visibility = Visibility.Visible;
                MessageBox.Show(this, NoticeText.Text, errorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            nextPoll = DateTimeOffset.UtcNow + delay;
            Render();
        }
        finally { busy = false; BusyText.Text = ""; UpdateButtons(); }
    }

    private bool RealPlayer()
    {
        if (busy) return false;
        if (!accountAuthenticated || !ready || player == Guid.Empty || gateway is DemoGame) { Notify("Entre com o token e ative um perfil real primeiro.", true); return false; }
        if (controller?.Pending is not null) { Notify("Resolva a intenção pendente antes de trocar de mesa.", true); return false; }
        return true;
    }

    private bool CanSwitch()
    {
        if (busy) return false;
        if (controller?.Pending is not null) { Notify("Resolva a intenção pendente antes de trocar de sessão.", true); return false; }
        return controller is null || MessageBox.Show(this,
            gateway is DemoGame ? "Iniciar outra sessão? A partida atual contra a CPU será descartada." :
                "Trocar de sessão? Sua ativação online será mantida para retomada.",
            "Trocar sessão", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void ShowAccessStep(int step)
    {
        EmailPanel.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        TokenPanel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        ProfilePanel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        var indicators = new[] { EmailStep, TokenStep, ProfileStep };
        for (int i = 0; i < indicators.Length; i++)
        {
            indicators[i].Background = new SolidColorBrush(i == step ? Color.FromRgb(75, 57, 108) : Color.FromRgb(37, 37, 50));
            indicators[i].Opacity = i <= step ? 1 : .5;
        }
        AccessNotice.Visibility = Visibility.Collapsed;
        if (step == 0) EmailBox.Focus();
        else if (step == 1) TokenBox.Focus();
        else ProfilesBox.Focus();
    }
    private void ShowToken_Click(object sender, RoutedEventArgs e) { if (!busy) ShowAccessStep(1); }
    private void ShowEmail_Click(object sender, RoutedEventArgs e) { if (!busy) ShowAccessStep(0); }

    private async void RequestToken_Click(object sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text;
        await RunAsync(async () => { await api.RequestTokenAsync(email, lifetime.Token); ShowAccessStep(1); Notify("Solicitação aceita. Confira o e-mail FIAP e o spam; o token não vem na resposta."); });
    }
    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSwitch()) return;
        var token = TokenBox.Password.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) token = token[7..].Trim();
        if (token.Length == 0) { Notify("Cole o token recebido por e-mail.", true); return; }
        await RunAsync(async () =>
        {
            var client = new DominoApiClient { Token = token };
            try
            {
                await client.ValidateSessionAsync(lifetime.Token);
                var profiles = await client.ProfilesAsync(lifetime.Token);
                if (RememberToken.IsChecked == true) accountVault.Set("token", token);
                api.Dispose(); api = client; gateway = api; controller = null; playerVault = null;
                player = Guid.Empty; ready = false; AutoBox.IsChecked = false;
                accountAuthenticated = true;
                SetProfiles(profiles); ClearGame(); ShowAccessStep(2);
                ModeText.Text = "BEM-VINDA AO CLUBE";
                Notify("Acesso confirmado. Escolha seu jogador ou crie um novo.");
            }
            catch { client.Dispose(); throw; }
        });
    }
    private async void RestoreToken_Click(object sender, RoutedEventArgs e) => await RunAsync(() =>
    {
        TokenBox.Password = accountVault.Get<string>("token") ?? "";
        if (TokenBox.Password.Length > 0) ShowAccessStep(1);
        Notify(TokenBox.Password.Length == 0 ? "Nenhum token salvo." : "Acesso recuperado. Clique em Continuar.");
        return Task.CompletedTask;
    });
    private void SetProfiles(PlayerProfile[] profiles)
    {
        ProfilesBox.ItemsSource = profiles;
        EnterLobbyButton.IsEnabled = profiles.Length > 0;
        ProfileHint.Text = profiles.Length == 0 ? "Crie seu primeiro jogador para entrar no salão." : "Escolha seu jogador para entrar no salão.";
        if (profiles.Length > 0) ProfilesBox.SelectedIndex = 0;
    }
    private async void Profiles_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(async () => SetProfiles(await api.ProfilesAsync(lifetime.Token)));
    private async void CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var name = Prompt("Novo jogador", "Nome do perfil (1 a 150 caracteres):");
        if (string.IsNullOrWhiteSpace(name)) return;
        await RunAsync(async () =>
        {
            var created = await api.CreateProfileAsync(name, lifetime.Token);
            SetProfiles(await api.ProfilesAsync(lifetime.Token));
            ProfilesBox.SelectedItem = ((PlayerProfile[])ProfilesBox.ItemsSource).FirstOrDefault(p => p.JogadorId == created.JogadorId);
            Notify("Jogador criado. Clique em Entrar no salão.");
        });
    }
    private async void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (busy || ProfilesBox.SelectedItem is not PlayerProfile profile) { Notify("Selecione um perfil."); return; }
        if (controller?.Pending is not null && profile.JogadorId != player) { Notify("Resolva a intenção pendente antes de trocar de perfil.", true); return; }
        await RunAsync(async () =>
        {
            var vault = accountVault.ForPlayer(profile.JogadorId);
            string key = vault.ActivationKey(); // Persistência antes do POST.
            await api.ActivateAsync(profile.JogadorId, key, lifetime.Token);
            gateway = api; player = profile.JogadorId; playerVault = vault;
            intentStore = new VaultIntentStore(vault);
            controller = new GameController(gateway, player, intentStore);
            map = vault.Get<ActionMap>("mapping") ?? new(); map.Validate();
            mapConfirmed = vault.Get<bool>("mappingConfirmed");
            ready = true; AutoBox.IsChecked = false;
            ClearGame(); ModeText.Text = "PRONTO PARA JOGAR";
            ProfileText.Text = profile.Nome ?? "Jogador";
            ProfileText.ToolTip = player.ToString();
            ProfileInitialText.Text = string.IsNullOrWhiteSpace(profile.Nome) ? "Q" : profile.Nome.Trim()[..1].ToUpperInvariant();
            MappingText.Text = mapConfirmed ? "Mapeamento confirmado por você. A API valida cada ação." : "Confirme com o professor os nomes dos códigos 0 a 5 e use Configurar ações da API.";
            UpdateNavigation();
            Tabs.SelectedItem = controller.Pending is null ? LobbyTab : GameTab;
            Notify(controller.Pending is null ? "Perfil ativado. Crie uma mesa ou use Retomar por ID." : "Intenção pendente recuperada. Use Repetir intenção pendente.");
        });
    }
    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (busy || gateway is DemoGame || !accountAuthenticated) return;
        busy = true; UpdateButtons();
        bool confirmed;
        try
        {
            confirmed = MessageBox.Show(this,
                "Sair da sessão neste computador? O acesso salvo será removido. A partida online continuará no servidor e poderá ser retomada após entrar novamente.",
                "Sair da sessão", MessageBoxButton.YesNo, MessageBoxImage.Question,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }
        finally { busy = false; UpdateButtons(); }
        if (!confirmed) return;
        await RunAsync(async () =>
        {
            // Preserve activation key, last match and pending intent for a later login.
            accountVault.Remove("token");
            AutoBox.IsChecked = false;
            bool releaseUnconfirmed = false;
            if (ready && player != Guid.Empty)
            {
                try { await api.ReleaseAsync(player, lifetime.Token); }
                catch (Exception) { releaseUnconfirmed = true; }
            }
            api.Token = ""; api.ActivationKey = "";
            accountAuthenticated = false; ready = false; mapConfirmed = false;
            controller = null; player = Guid.Empty; playerVault = null;
            intentStore = new MemoryIntentStore(); map = new();
            turnClock.Reset(); ClearGame(); LogBox.Clear();
            TokenBox.Clear(); SetProfiles([]); MatchesGrid.ItemsSource = null;
            ProfileText.Text = "Nenhum perfil ativo"; ProfileText.ToolTip = null;
            ProfileInitialText.Text = "Q";
            Tabs.SelectedItem = AccessTab; ShowAccessStep(0);
            ModeText.Text = "SEU LUGAR À MESA";
            Notify(releaseUnconfirmed
                ? "Você saiu neste computador. Não foi possível confirmar a liberação do perfil no servidor. A partida online não foi encerrada."
                : "Você saiu da sessão. Entre novamente para retomar sua partida online.", releaseUnconfirmed);
        });
    }
    private async void Release_Click(object sender, RoutedEventArgs e)
    {
        if (!RealPlayer()) return;
        if (MessageBox.Show(this, "Liberar o perfil para outro cliente? Isso não encerra a partida.", "Liberar perfil", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await RunAsync(async () =>
        {
            await api.ReleaseAsync(player, lifetime.Token);
            controller = null; player = Guid.Empty; ready = false; AutoBox.IsChecked = false;
            ClearGame(); ProfileText.Text = "Nenhum perfil ativo neste cliente.";
            Tabs.SelectedItem = AccessTab; ShowAccessStep(2);
            Notify("Perfil liberado com confirmação do servidor.");
        });
    }

    private async void Matches_Click(object sender, RoutedEventArgs e)
    {
        if (!RealPlayer()) return;
        await RunAsync(async () =>
        {
            var matches = await api.AvailableAsync(lifetime.Token);
            MatchesGrid.ItemsSource = matches;
            EmptyMatchesText.Visibility = matches.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyMatchesText.Text = "Nenhuma mesa aberta agora. Crie uma partida para começar.";
            Notify($"{matches.Length} mesa(s) disponível(is).");
        });
    }
    private async void CreateMatch_Click(object sender, RoutedEventArgs e)
    {
        CreateMatchNotice.Visibility = Visibility.Visible;
        if (busy)
        {
            CreateMatchNotice.Text = "Aguarde a operação atual terminar e tente novamente.";
            return;
        }
        if (!RealPlayer())
        {
            CreateMatchNotice.Text = NoticeText.Text;
            MessageBox.Show(this, NoticeText.Text, "Antes de criar uma partida", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!int.TryParse(TargetBox.Text, out int target) || target <= 0)
        {
            CreateMatchNotice.Text = "Informe uma meta inteira positiva.";
            TargetBox.Focus();
            return;
        }
        if (controller?.State is { Situacao: not MatchStatus.Finished } && MessageBox.Show(this, "A mesa atual continuará existindo. Criar outra?", "Nova mesa", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            CreateMatchNotice.Text = "Criação cancelada. Sua mesa atual continua disponível na aba Na mesa.";
            return;
        }
        CreateMatchButton.IsEnabled = false;
        CreateMatchButton.Content = "Criando mesa…";
        CreateMatchNotice.Text = "Enviando a solicitação. Aguarde a resposta do servidor.";
        try
        {
            await RunAsync(async () =>
            {
                var state = await api.CreateAsync(player, target, lifetime.Token);
                // Preserve o ID antes de renderizar, inclusive se a apresentação falhar.
                playerVault?.Set("lastMatch", state.PartidaId);
                CreateMatchNotice.Text = $"Mesa criada. ID: {state.PartidaId}";
                OpenState(state);
            }, "Não foi possível abrir a partida");
        }
        finally
        {
            CreateMatchButton.Content = "+  Criar partida";
            CreateMatchButton.IsEnabled = true;
        }
    }
    private void OpenState(GameState state)
    {
        controller ??= new GameController(gateway, player, intentStore);
        controller.Open(state); playerVault?.Set("lastMatch", state.PartidaId);
        AutoBox.IsChecked = false; UpdateNavigation(); Tabs.SelectedItem = GameTab;
        Render(); Notify("Estado confirmado recebido."); nextPoll = DateTimeOffset.UtcNow.AddSeconds(1.5);
    }
    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        if (!RealPlayer()) return;
        if (MatchesGrid.SelectedItem is not AvailableMatch match) { Notify("Selecione uma mesa."); return; }
        await RunAsync(async () =>
        {
            controller = new GameController(gateway, player, intentStore);
            controller.Prepare(match.PartidaId, "entrar", match.VersaoEstado);
            await controller.SendPendingAsync(lifetime.Token); AfterCommand();
        });
    }
    private async void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (!RealPlayer()) return;
        await RunAsync(async () =>
        {
            var last = playerVault?.Get<Guid?>("lastMatch");
            var value = Prompt("Retomar partida", "ID completo da partida:", last?.ToString() ?? "");
            if (value is null) return;
            if (!Guid.TryParse(value, out var id)) throw new ArgumentException("Informe um UUID válido.");
            OpenState(await api.GetStateAsync(id, player, ct: lifetime.Token) ?? throw new InvalidOperationException("Estado vazio."));
        });
    }
    private async Task PerformAsync(string action, Tile? tile = null, Side side = Side.Right)
    {
        if (busy || !ready || gateway is DemoGame { AwaitingNextRound: true } || controller?.State is not { } state) return;
        await RunAsync(async () =>
        {
            controller.Prepare(state.PartidaId, action, state.VersaoEstado, tile, side);
            await controller.SendPendingAsync(lifetime.Token); AfterCommand();
        });
    }
    private void AfterCommand()
    {
        if (controller?.State is { } state) playerVault?.Set("lastMatch", state.PartidaId);
        UpdateNavigation(); Tabs.SelectedItem = GameTab; Render();
        Notify(gateway is DemoGame ? "Jogada confirmada na partida contra a CPU." : "Ação confirmada pelo servidor.");
        nextPoll = DateTimeOffset.UtcNow.AddSeconds(0.8);
    }
    private async void Left_Click(object sender, RoutedEventArgs e) => await PlayAtEndAsync(Side.Left);
    private async void Right_Click(object sender, RoutedEventArgs e) => await PlayAtEndAsync(Side.Right);
    private async Task PlayAtEndAsync(Side side)
    {
        if (!CanChooseTile() || selected is not { } tile || controller?.State is not { } state) return;
        if (!MoveRules.LegalMoves(state).Any(m => m.Tile == tile && m.Side == side))
        {
            ActionText.Text = "Essa peça não encaixa nessa ponta. Escolha outra peça.";
            return;
        }
        await PerformAsync("jogar", tile, side);
    }
    private bool CanChooseTile() => ready && !busy && gateway is not DemoGame { AwaitingNextRound: true } && mapConfirmed && controller?.Pending is null &&
        controller?.State is { Situacao: MatchStatus.Playing } state && state.JogadorDoTurnoId == player &&
        (state.ProximaAcao == map.Open || state.ProximaAcao == map.Play);

    private async void Buy_Click(object sender, RoutedEventArgs e) => await PerformAsync("comprar");
    private async void Pass_Click(object sender, RoutedEventArgs e) => await PerformAsync("passar");
    private async void Start_Click(object sender, RoutedEventArgs e) => await PerformAsync("iniciar");
    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !ready || controller?.Pending is null) return;
        await RunAsync(async () => { await controller.SendPendingAsync(lifetime.Token); AfterCommand(); });
    }

    private void Auto_Click(object sender, RoutedEventArgs e)
    {
        if (AutoBox.IsChecked == true && (!mapConfirmed || controller?.State is null))
        {
            AutoBox.IsChecked = false; Notify("Entre em uma partida e confirme o mapeamento de ações antes de ativar o bot.", true); return;
        }
        Notify(AutoBox.IsChecked == true ? "Jogador autônomo ativado." : "Controle manual ativado.");
    }
    private void Suggest_Click(object sender, RoutedEventArgs e)
    {
        if (controller?.State is not { } state || busy) return;
        if (!mapConfirmed) { Notify("Confirme os códigos da API na sala de partidas.", true); return; }
        var decision = bot.Choose(state, player, map);
        if (decision is null) { Notify("Sem decisão segura para este estado."); return; }
        selected = decision.Tile; DrawHand(); UpdateButtons();
        Notify($"Sugestão: {decision.Action} {decision.Tile} {(decision.Tile is null ? "" : decision.Side == Side.Left ? "à esquerda" : "à direita")}. {decision.Reason}");
    }
    private async void Mapping_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        await RunAsync(() =>
        {
            var value = Prompt("Confirmar códigos da API",
                "O Swagger lista 0..5 sem os nomes. Confirme a ordem com o professor.\nInforme: aguardar, abertura, jogar, comprar, passar, encerrar.\nHipótese inicial NÃO comprovada: 0,1,2,3,4,5",
                string.Join(",", map.Wait, map.Open, map.Play, map.Buy, map.Pass, map.End));
            if (value is null) return Task.CompletedTask;
            var numbers = value.Split(',').Select(v => int.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
            if (numbers.Length != 6) throw new ArgumentException("Informe seis números separados por vírgula.");
            var mapping = new ActionMap(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5]); mapping.Validate();
            playerVault?.Set("mapping", mapping); playerVault?.Set("mappingConfirmed", true);
            map = mapping; mapConfirmed = true;
            MappingText.Text = "Mapeamento confirmado por você. A API continua sendo a autoridade das regras.";
            Render(); Notify("Mapeamento atualizado."); return Task.CompletedTask;
        });
    }
    private async void Demo_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSwitch()) return;
        if (gateway is not DemoGame && savedCpu is not null &&
            MessageBox.Show(this, "Iniciar outra partida substituirá o jogo contra a CPU salvo. Continuar?",
                "Nova partida", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await RunAsync(() => { StartTraining(); return Task.CompletedTask; });
    }
    private void StartTraining()
    {
        int choice = int.Parse(((ComboBoxItem)CpuDuration.SelectedItem).Tag.ToString()!, CultureInfo.InvariantCulture);
        int? rounds = CpuDuration.SelectedIndex >= 3 ? choice : null;
        AttachCpu(new DemoGame(target: rounds.HasValue ? 15 : choice,
            difficulty: EasyCpu.IsChecked == true ? CpuDifficulty.Easy : CpuDifficulty.Normal,
            roundLimit: rounds, pauseBetweenRounds: true));
        Notify("Partida contra a CPU iniciada. Boa sorte!");
    }
    private void AttachCpu(DemoGame demo)
    {
        AutoBox.IsChecked = false; turnClock.Reset(); ClearGame();
        EasyCpu.IsChecked = demo.Difficulty == CpuDifficulty.Easy;
        NormalCpu.IsChecked = demo.Difficulty == CpuDifficulty.Normal;
        CpuDuration.SelectedIndex = demo.RoundLimit switch { 3 => 3, 5 => 4, 7 => 5, _ => demo.Target == 30 ? 1 : demo.Target == 50 ? 2 : 0 };
        gateway = demo; player = demo.PlayerId; playerVault = null;
        intentStore = new MemoryIntentStore(); controller = new GameController(demo, player, intentStore);
        map = new(); mapConfirmed = true; ready = true;
        string duration = demo.RoundLimit is int limit ? $"{limit} rodadas" : $"meta de {demo.Target} pontos";
        ModeText.Text = $"Contra a CPU — {(demo.Difficulty == CpuDifficulty.Easy ? "Fácil" : "Normal")} — {duration}";
        LocalRulesText.Text = "Modo local contra a CPU: " + duration +
            ". Pontas somando múltiplos de 5 rendem soma ÷ 5 pontos. Bônus local simplificado: +1 para a menor soma restante; empate sem bônus. Por rodadas, vence o maior placar; placares iguais dão empate. As regras online são determinadas pela API.";
        OpenState(demo.View(player));
    }
    private bool SaveCpu()
    {
        if (gateway is not DemoGame demo) return true;
        try
        {
            var snapshot = demo.Save();
            accountVault.Set("cpuGame", snapshot); savedCpu = snapshot; saveFailed = false;
            SaveStateText.Text = "Partida salva neste computador";
            return true;
        }
        catch (Exception)
        {
            saveFailed = true; SaveStateText.Text = "Falha ao salvar: mantenha o app aberto";
            Notify("Não foi possível salvar a partida neste computador. Mantenha o app aberto e tente novamente antes de sair.", true);
            return false;
        }
    }
    private async void NextRound_Click(object sender, RoutedEventArgs e)
    {
        if (busy || gateway is not DemoGame { AwaitingNextRound: true } demo) return;
        await RunAsync(() =>
        {
            demo.ContinueRound(); controller!.Apply(demo.View(player)); Render();
            nextPoll = DateTimeOffset.UtcNow.AddSeconds(1.5);
            return Task.CompletedTask;
        });
    }
    private async void RestartTraining_Click(object sender, RoutedEventArgs e)
    {
        if (busy || gateway is not DemoGame || controller?.Pending is not null) return;
        if (controller?.State is { Situacao: not MatchStatus.Finished } &&
            MessageBox.Show(this, "Reiniciar a partida contra a CPU? O placar e as peças atuais serão descartados.",
                "Reiniciar partida", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunAsync(() => { StartTraining(); return Task.CompletedTask; });
    }
    private void EndTraining_Click(object sender, RoutedEventArgs e)
    {
        if (busy || gateway is not DemoGame || controller?.State is null || controller.Pending is not null) return;
        // Pause polling while the modal dialog runs its nested dispatcher loop.
        busy = true; turnClock.Stop(); UpdateButtons();
        try
        {
            if (MessageBox.Show(this,
                "Encerrar a partida contra a CPU? O placar e as peças serão descartados e você voltará ao início.",
                "Encerrar partida", MessageBoxButton.YesNo, MessageBoxImage.Question,
                MessageBoxResult.No) != MessageBoxResult.Yes) return;
            accountVault.Remove("cpuGame"); savedCpu = null; saveFailed = false;
            AutoBox.IsChecked = false;
            controller = null; gateway = api; player = Guid.Empty; playerVault = null;
            intentStore = new MemoryIntentStore(); ready = false; mapConfirmed = false; map = new();
            turnClock.Reset(); ClearGame(); LogBox.Clear();
            Tabs.SelectedItem = AccessTab;
            ModeText.Text = "SEU LUGAR À MESA";
            Notify("Partida encerrada. Você pode começar uma nova partida contra a CPU ou jogar online.");
        }
        catch (Exception) { Notify("Não foi possível remover o jogo salvo. A partida foi mantida; tente novamente.", true); }
        finally
        {
            busy = false;
            if (gateway is DemoGame && controller?.State?.Situacao == MatchStatus.Playing && Tabs.SelectedItem == GameTab)
                turnClock.Start();
            UpdateButtons();
        }
    }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        AutoBox.IsChecked = false;
        if (gateway is DemoGame)
        {
            turnClock.Stop();
            Tabs.SelectedItem = AccessTab;
            Notify(controller?.State?.Situacao == MatchStatus.Finished ? "Escolha seu próximo desafio." : "Partida pausada. Use Continuar minha partida para voltar à mesa.");
        }
        else
        {
            Tabs.SelectedItem = LobbyTab.Visibility == Visibility.Visible ? LobbyTab : AccessTab;
            Notify("Você voltou ao salão. A partida online continua; retome pela aba Na mesa.");
        }
    }
    private async void ResumeTraining_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (gateway is DemoGame && controller?.State is not null)
        {
            Tabs.SelectedItem = GameTab;
            if (controller.State.Situacao == MatchStatus.Playing) turnClock.Start();
            return;
        }
        if (savedCpu is null || !CanSwitch()) return;
        await RunAsync(() =>
        {
            AttachCpu(DemoGame.Restore(savedCpu));
            Notify("Partida salva recuperada. Continue de onde parou.");
            return Task.CompletedTask;
        });
    }

    private async void Tick(object? sender, EventArgs e)
    {
        if (controller?.State is not { } state) return;
        if (gateway is DemoGame)
        {
            if (Tabs.SelectedItem != GameTab) { turnClock.Stop(); return; }
            if (state.Situacao == MatchStatus.Playing) turnClock.Start();
        }
        if (gateway is DemoGame { AwaitingNextRound: true }) { turnClock.Stop(); return; }
        var elapsed = state.JogadorDoTurnoId == player && state.Situacao == MatchStatus.Playing ? turnClock.Elapsed : TimeSpan.Zero;
        ClockText.Text = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
        if (!ready || busy || controller.Pending is not null || state.Situacao == MatchStatus.Finished || DateTimeOffset.UtcNow < nextPoll) return;
        nextPoll = DateTimeOffset.UtcNow.AddSeconds(1.5);
        if (gateway is DemoGame demo && state.JogadorDoTurnoId != player)
        {
            await RunAsync(async () => { await demo.StepOpponentAsync(); controller.Apply(demo.View(player)); Render(); });
        }
        else if (AutoBox.IsChecked == true && mapConfirmed && state.JogadorDoTurnoId == player)
        {
            var decision = bot.Choose(state, player, map);
            if (decision is null) { AutoBox.IsChecked = false; Notify("Bot pausado: nenhuma decisão segura para este estado.", true); }
            else await PerformAsync(decision.Action, decision.Tile, decision.Side);
        }
        else await RunAsync(async () => { if (await controller.RefreshAsync(lifetime.Token)) Render(); });
    }

    private void Render()
    {
        if (controller?.State is not { } state) return;
        var turn = (state.PartidaId, state.NumeroRodada, state.JogadorDoTurnoId);
        if (lastTurn != turn) { lastTurn = turn; turnClock.Restart(); }
        if (selected is not null && !state.Hand.Contains(selected)) selected = null;
        MatchText.Text = $"Rodada {state.NumeroRodada}, versão {state.VersaoEstado}\nMesa {state.PartidaId.ToString()[..8]}";
        MatchText.ToolTip = state.PartidaId.ToString();
        ScoreText.Text = string.Join("     ×     ", state.Players.Select(p => $"{p.Nome ?? "Jogador"}  {p.Pontuacao} pts  ({p.QuantidadePedrasNaMao} pedras)"));
        ActionText.Text = map.Label(state.ProximaAcao);
        if (state.Situacao == MatchStatus.Waiting) ActionText.Text = state.Players.Count == 2 ? "Dois jogadores presentes. Clique em Iniciar partida." : "Aguardando o segundo jogador";
        if (state.Situacao == MatchStatus.Finished)
        {
            ActionText.Text = "Partida encerrada. Vencedor: " + (state.Players.FirstOrDefault(p => p.JogadorId == state.VencedorId)?.Nome ?? state.VencedorId?.ToString() ?? "não informado");
            AutoBox.IsChecked = false;
        }
        else if (state.PedraAberturaObrigatoria is { } opening) ActionText.Text += $" Abertura obrigatória: {opening}";
        if (!mapConfirmed && state.Situacao == MatchStatus.Playing) ActionText.Text = "Configure as ações da API no salão para habilitar suas jogadas.";
        DrawScoreCards(state);
        var opponent = state.Players.FirstOrDefault(p => p.JogadorId != player);
        OpponentNameText.Text = opponent?.Nome ?? "Aguardando adversário";
        OpponentCountText.Text = opponent is null ? "Convide alguém para sua mesa" : $"{opponent.QuantidadePedrasNaMao} {(opponent.QuantidadePedrasNaMao == 1 ? "peça" : "peças")} na mão";
        OpponentTiles.Children.Clear();
        // Apenas a quantidade pública; nenhuma face adversária é consultada.
        for (int i = 0; i < Math.Min(7, opponent?.QuantidadePedrasNaMao ?? 0); i++)
            OpponentTiles.Children.Add(new Border { Width = 30, Height = 53, CornerRadius = new CornerRadius(5), Margin = new Thickness(2), Background = new SolidColorBrush(Color.FromRgb(41, 36, 57)), BorderBrush = new SolidColorBrush(Color.FromRgb(137, 118, 172)), BorderThickness = new Thickness(1), Child = new TextBlock { Text = "Q", FontFamily = new FontFamily("Segoe UI"), Foreground = Brushes.SlateGray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        TurnText.Text = state.Situacao == MatchStatus.Finished ? "Partida encerrada" : state.Situacao == MatchStatus.Waiting ? "Aguardando o início" : state.JogadorDoTurnoId == player ? "Sua vez de jogar" : "Vez do adversário";
        TurnBadge.Background = new SolidColorBrush(state.Situacao == MatchStatus.Playing && state.JogadorDoTurnoId == player ? Color.FromRgb(51, 103, 89) : Color.FromRgb(57, 51, 77));
        EmptyBoardPanel.Visibility = state.Board.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BoardPanel.ClearTiles();
        EndsText.Text = state.Board.Count == 0 ? "A MESA ESTÁ PRONTA" : $"Pontas abertas: {state.Board[0].ValorA} e {state.Board[^1].ValorB}";
        for (int i = 0; i < state.Board.Count; i++)
        {
            var tile = state.Board[i];
            var stone = new TileView(tile, vertical: tile.ValorA == tile.ValorB, selected: i == 0 || i == state.Board.Count - 1);
            var stack = new Grid { Margin = new Thickness(0, 0, 1, 0), Height = 126, VerticalAlignment = VerticalAlignment.Center };
            stone.LayoutTransform = new ScaleTransform(1.35, 1.35);
            stone.VerticalAlignment = VerticalAlignment.Center;
            stack.Children.Add(stone);
            BoardPanel.Children.Add(stack);
        }
        DrawHand(); UpdateButtons();
        RenderExperience(state);
        if (gateway is DemoGame && (saveFailed || renderedState?.PartidaId != state.PartidaId || renderedState.VersaoEstado != state.VersaoEstado)) SaveCpu();
        renderedState = state;
    }
    private static void AnimateArrival(UIElement element)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(240)) { FillBehavior = FillBehavior.Stop });
        var shift = new TranslateTransform();
        element.RenderTransform = shift;
        shift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(240)) { FillBehavior = FillBehavior.Stop });
    }
    private void RenderExperience(GameState state)
    {
        bool waiting = state.Situacao == MatchStatus.Waiting;
        bool finished = state.Situacao == MatchStatus.Finished;
        WaitingPanel.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed;
        WaitingTitle.Text = state.Players.Count >= 2 ? "Tudo pronto para começar" : "Aguardando outro jogador";
        WaitingMatchId.Text = state.PartidaId.ToString();
        FinishPanel.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;
        bool roundBreak = gateway is DemoGame { AwaitingNextRound: true };
        GameOverlay.Visibility = finished || roundBreak ? Visibility.Visible : Visibility.Collapsed;
        RoundPanel.Visibility = roundBreak ? Visibility.Visible : Visibility.Collapsed;
        if (roundBreak && gateway is DemoGame { LastRound: { } summary } local)
        {
            AutoBox.IsChecked = false; turnClock.Stop();
            RoundTitle.Text = $"Rodada {summary.Number} concluída";
            string bonus = summary.Winner is null ? "Empate na soma restante: sem bônus" :
                $"{(summary.Winner == local.PlayerId ? "Você" : "CPU")} recebe +{summary.Bonus} de bônus local pela menor soma restante";
            RoundDetail.Text = $"{summary.Reason}\n{bonus}\n\nPontos nesta rodada: você +{summary.PlayerPoints} | CPU +{summary.CpuPoints}\nPlacar: você {summary.PlayerTotal} × CPU {summary.CpuTotal}";
            TurnText.Text = "Intervalo entre rodadas"; ActionText.Text = "Confira o resultado e avance quando quiser";
        }
        ClockLabel.Text = waiting ? "ESPERA" : finished ? "FINAL" : state.JogadorDoTurnoId == player ? "SUA VEZ" : "ADVERSÁRIO";
        if (finished)
        {
            turnClock.Stop();
            FinishTitle.Text = state.VencedorId == player ? "Você venceu!" : state.VencedorId.HasValue ? "Desta vez não deu" : gateway is DemoGame ? "Empate!" : "Partida encerrada";
            FinishScore.Text = string.Join("   ×   ", state.Players.OrderByDescending(p => p.JogadorId == player)
                .Select(p => $"{(p.JogadorId == player ? "Você" : p.Nome ?? "Adversário")}: {p.Pontuacao}"));
            FinishDetail.Text = gateway is DemoGame { LastRound: { } last } ?
                $"Rodada {last.Number}: {last.Reason}\nPontos da rodada: você +{last.PlayerPoints} | CPU +{last.CpuPoints}" : "Resultado confirmado pelo servidor";
            PlayAgainButton.Visibility = gateway is DemoGame ? Visibility.Visible : Visibility.Collapsed;
            FinishBackButton.Content = gateway is DemoGame ? "Voltar ao início" : "Voltar ao salão";
            if (renderedState?.PartidaId != state.PartidaId || renderedState.Situacao != MatchStatus.Finished)
                AnimateArrival(FinishPanel);
        }
        var before = renderedState;
        if (before is null || before.PartidaId != state.PartidaId || before.VersaoEstado >= state.VersaoEstado) return;
        var gains = state.Players.Select(p => new { Player = p, Gain = p.Pontuacao - (before.Players.FirstOrDefault(b => b.JogadorId == p.JogadorId)?.Pontuacao ?? p.Pontuacao) })
            .Where(p => p.Gain > 0).Select(p => $"{(p.Player.JogadorId == player ? "Você marcou" : (p.Player.Nome ?? "Adversário") + " marcou")} {p.Gain} {(p.Gain == 1 ? "ponto" : "pontos")}!").ToArray();
        string? feedback = gains.Length > 0 ? string.Join("  ", gains) : null;
        if (state.NumeroRodada != before.NumeroRodada)
            feedback = (feedback is null ? "" : feedback + "  ") + $"Rodada {state.NumeroRodada}";
        else
        {
            foreach (Button button in HandPanel.Children)
                if (button.Content is TileView && HandPanel.Children.IndexOf(button) < state.Hand.Count &&
                    !before.Hand.Any(t => t.Same(state.Hand[HandPanel.Children.IndexOf(button)]))) AnimateArrival(button);
            if (state.Hand.Count > before.Hand.Count) feedback ??= "Você comprou uma peça";
            for (int i = 0; i < state.Board.Count; i++)
                if (!before.Board.Any(t => t.Same(state.Board[i])) && BoardPanel.Children[i + 2] is Grid tileHost)
                    AnimateArrival(tileHost.Children[0]);
        }
        if (feedback is not null)
        {
            MoveFeedback.Text = feedback;
            MoveFeedback.BeginAnimation(UIElement.OpacityProperty, null);
            MoveFeedback.Opacity = 1;
            MoveFeedback.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromSeconds(3) });
        }
    }
    private void MatchSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (MatchesGrid.SelectedItem is AvailableMatch match)
        {
            SelectedMatchText.Text = $"Até {match.PontuacaoAlvo} pontos";
            SelectedMatchDetail.Text = $"Uma vaga disponível\nMesa {match.PartidaId.ToString()[..8]}";
        }
        else
        {
            SelectedMatchText.Text = "Selecione uma partida";
            SelectedMatchDetail.Text = "As vagas são confirmadas no ingresso.";
        }
    }

    private void DrawScoreCards(GameState state)
    {
        ScoreCards.Children.Clear();
        foreach (var participant in state.Players.OrderByDescending(p => p.JogadorId == player))
        {
            bool active = state.Situacao == MatchStatus.Playing && state.JogadorDoTurnoId == participant.JogadorId;
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = participant.JogadorId == player ? "VOCÊ" : "ADVERSÁRIO", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(156, 177, 189)) });
            content.Children.Add(new TextBlock { Text = participant.Nome ?? "Jogador", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
            content.Children.Add(new TextBlock { Text = participant.Pontuacao.ToString("00"), FontSize = 32, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(181, 160, 255)) });
            content.Children.Add(new TextBlock { Text = active ? "●  NA VEZ" : $"{participant.QuantidadePedrasNaMao} pedras", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(153, 188, 188)) });
            ScoreCards.Children.Add(new Border { Child = content, Padding = new Thickness(14), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(29, 29, 40)), BorderBrush = new SolidColorBrush(active ? Color.FromRgb(128, 223, 196) : Color.FromRgb(57, 53, 72)), BorderThickness = new Thickness(1) });
        }
    }

    private void DrawHand()
    {
        HandPanel.Children.Clear();
        if (controller?.State is not { } state) return;
        var compatible = MoveRules.LegalMoves(state).Select(m => m.Tile).ToHashSet();
        HandText.Text = $"Sua mão ({state.Hand.Count} {(state.Hand.Count == 1 ? "peça" : "peças")})";
        bool canChoose = ready && mapConfirmed && state.Situacao == MatchStatus.Playing &&
            state.JogadorDoTurnoId == player && (state.ProximaAcao == map.Open || state.ProximaAcao == map.Play);
        foreach (var tile in state.Hand)
        {
            bool available = canChoose && compatible.Contains(tile);
            var button = new Button { Content = new TileView(tile, vertical: false, selected: tile == selected) { LayoutTransform = new ScaleTransform(1.18, 1.18) }, Cursor = Cursors.Hand, Padding = new Thickness(3), Margin = new Thickness(0, 0, 5, 2), Background = tile == selected ? new SolidColorBrush(Color.FromRgb(77, 58, 108)) : Brushes.Transparent,
                Opacity = canChoose && !available ? 0.55 : 1,
                BorderBrush = available ? new SolidColorBrush(Color.FromRgb(128, 223, 196)) : Brushes.Transparent,
                ToolTip = available ? $"Peça {tile}: clique para iluminar os encaixes." : canChoose ? "Esta peça não está disponível para jogar agora." : "Aguarde sua vez para jogar." };
            button.RenderTransform = new TranslateTransform(0, tile == selected ? -5 : 0);
            System.Windows.Automation.AutomationProperties.SetName(button, $"Pedra {tile.ValorA} e {tile.ValorB}");
            button.Click += (_, _) =>
            {
                if (!CanChooseTile()) { ActionText.Text = "Aguarde sua vez ou a confirmação da ação atual."; return; }
                selected = selected == tile ? null : tile;
                DrawHand(); UpdateButtons();
                if (LeftButton.IsEnabled) LeftButton.BringIntoView();
                else if (RightButton.IsEnabled) RightButton.BringIntoView();
            };
            HandPanel.Children.Add(button);
        }
    }
    private void UpdateNavigation()
    {
        bool localTraining = gateway is DemoGame;
        bool onlineReady = accountAuthenticated && ready && player != Guid.Empty && !localTraining;
        bool hasTable = controller?.State is not null || controller?.Pending is not null;
        bool showTable = hasTable && ready && (localTraining || onlineReady);

        // Se a sessão perder acesso, volte à entrada antes de ocultar sua aba.
        if ((Tabs.SelectedItem == LobbyTab && !onlineReady) ||
            (Tabs.SelectedItem == GameTab && !showTable))
            Tabs.SelectedItem = AccessTab;
        LobbyTab.Visibility = onlineReady ? Visibility.Visible : Visibility.Collapsed;
        GameTab.Visibility = showTable ? Visibility.Visible : Visibility.Collapsed;
        GameTab.Header = localTraining ? "Contra a CPU" : "Na mesa";
    }

    private void UpdateButtons()
    {
        if (!IsInitialized) return;
        UpdateNavigation();
        var state = controller?.State;
        bool training = gateway is DemoGame;
        LogoutButton.Visibility = accountAuthenticated && !training ? Visibility.Visible : Visibility.Collapsed;
        LogoutButton.IsEnabled = !busy;
        BackButton.Content = training ? "← Voltar ao início" : "← Voltar ao salão";
        BackButton.IsEnabled = !busy;
        PlayAgainButton.IsEnabled = FinishBackButton.IsEnabled = !busy;
        LocalRulesText.Visibility = training ? Visibility.Visible : Visibility.Collapsed;
        RestartTrainingButton.Visibility = training && state is not null ? Visibility.Visible : Visibility.Collapsed;
        RestartTrainingButton.IsEnabled = !busy && controller?.Pending is null;
        EndTrainingButton.Visibility = training && state is not null ? Visibility.Visible : Visibility.Collapsed;
        EndTrainingButton.IsEnabled = !busy && controller?.Pending is null;
        ResumeTrainingButton.Visibility = (training && state is not null) || savedCpu is not null ? Visibility.Visible : Visibility.Collapsed;
        ResumeTrainingButton.Content = state?.Situacao == MatchStatus.Finished ? "Ver resultado da partida" : "Continuar minha partida  →";
        ResumeTrainingButton.IsEnabled = !busy;
        SaveStateText.Text = saveFailed ? "Falha ao salvar: mantenha o app aberto" : savedCpu is not null ? "Partida salva neste computador" : "Seu progresso contra a CPU será salvo automaticamente";
        bool atBreak = gateway is DemoGame { AwaitingNextRound: true };
        bool finished = state?.Situacao == MatchStatus.Finished;
        HintButton.Visibility = AutoBox.Visibility = finished || atBreak ? Visibility.Collapsed : Visibility.Visible;
        RestartTrainingButton.Visibility = training && state is not null && !finished ? Visibility.Visible : Visibility.Collapsed;
        EndTrainingButton.Visibility = training && state is not null && !finished ? Visibility.Visible : Visibility.Collapsed;
        NextRoundButton.IsEnabled = !busy;
        bool enabled = ready && !busy && !atBreak && controller?.Pending is null && state is not null;
        bool turn = enabled && mapConfirmed && state!.Situacao == MatchStatus.Playing && state.JogadorDoTurnoId == player;
        bool play = turn && (state!.ProximaAcao == map.Open || state.ProximaAcao == map.Play);
        Move[] moves = state is null ? [] : MoveRules.LegalMoves(state).ToArray();
        LeftButton.IsEnabled = play && moves.Any(m => m.Tile == selected && m.Side == Side.Left);
        RightButton.IsEnabled = play && moves.Any(m => m.Tile == selected && m.Side == Side.Right);
        bool showOptions = ready && !atBreak && mapConfirmed && controller?.Pending is null && state is not null &&
            state.Situacao == MatchStatus.Playing && state.JogadorDoTurnoId == player &&
            (state.ProximaAcao == map.Open || state.ProximaAcao == map.Play);
        LeftButton.Visibility = showOptions && moves.Any(m => m.Tile == selected && m.Side == Side.Left) ? Visibility.Visible : Visibility.Collapsed;
        RightButton.Visibility = showOptions && moves.Any(m => m.Tile == selected && m.Side == Side.Right) ? Visibility.Visible : Visibility.Collapsed;
        LeftEndLabel.Text = state?.Board.Count > 0 ? $"Encaixar\nno {state.Board[0].ValorA}" : "Encaixar aqui";
        RightEndLabel.Text = state?.Board.Count > 0 ? $"Encaixar\nno {state.Board[^1].ValorB}" : "Começar com\nesta peça";
        EmptyBoardPanel.Visibility = state?.Board.Count == 0 && RightButton.Visibility != Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        foreach (var handButton in HandPanel.Children.OfType<Button>()) handButton.IsEnabled = enabled;

        BuyButton.IsEnabled = turn && state!.ProximaAcao == map.Buy;
        BuyButton.ToolTip = BuyButton.IsEnabled ? "Comprar uma peça do monte" : "Disponível quando a API indicar que você deve comprar.";
        PassButton.ToolTip = turn && state!.ProximaAcao == map.Pass ? "Passar a vez" : "Disponível quando a API permitir passar.";
        if (play) ActionText.Text = selected is null ? "Escolha uma peça destacada para ver os encaixes na mesa" :
            LeftButton.IsEnabled || RightButton.IsEnabled ? $"Peça {selected}: clique no encaixe iluminado na mesa" :
            state!.PedraAberturaObrigatoria is { } required && state.Board.Count == 0 ? $"Comece com a peça {required}" : "Essa peça não encaixa nas pontas. Escolha uma peça destacada";
        PassButton.IsEnabled = turn && state!.ProximaAcao == map.Pass;
        StartButton.IsEnabled = enabled && state!.Situacao == MatchStatus.Waiting && state.Players.Count == 2;
        RetryButton.IsEnabled = ready && !busy && controller?.Pending is not null;
        RetryButton.Visibility = controller?.Pending is null ? Visibility.Collapsed : Visibility.Visible;
        StartButton.Visibility = state?.Situacao == MatchStatus.Waiting ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ClearGame()
    {
        renderedState = null;
        WaitingPanel.Visibility = FinishPanel.Visibility = RoundPanel.Visibility = GameOverlay.Visibility = Visibility.Collapsed;
        MoveFeedback.BeginAnimation(UIElement.OpacityProperty, null); MoveFeedback.Opacity = 0; MoveFeedback.Text = "";
        selected = null; BoardPanel.ClearTiles(); HandPanel.Children.Clear();
        MatchText.Text = "Nenhuma partida selecionada"; ScoreText.Text = "SUA MESA";
        ActionText.Text = "Crie ou entre em uma partida."; ClockText.Text = "00:00"; lastTurn = null;
        ScoreCards.Children.Clear(); EmptyBoardPanel.Visibility = Visibility.Visible;
        OpponentTiles.Children.Clear(); TurnText.Text = "Prepare sua próxima jogada";
        OpponentNameText.Text = "Aguardando adversário"; OpponentCountText.Text = "A outra mão permanece privada";
    }
    private async void Result_Click(object sender, RoutedEventArgs e)
    {
        if (busy || controller?.State is not { } state) return;
        await RunAsync(async () =>
        {
            var result = await gateway.ResultAsync(state.PartidaId, player, lifetime.Token);
            string Name(Guid id) => result.Participantes?.FirstOrDefault(p => p.JogadorId == id)?.Nome ?? id.ToString();
            var lines = new List<string> { $"Mesa: {result.PartidaId}", $"Situação: {result.Situacao} • Versão: {result.VersaoEstado}",
                $"Vencedor: {(result.VencedorId.HasValue ? Name(result.VencedorId.Value) : "Ainda não definido")}", "" };
            lines.AddRange((result.Participantes ?? []).Select(p => $"{p.Nome}: {p.Pontuacao} pontos")); lines.Add("");
            lines.AddRange((result.Eventos ?? []).Select(ev => $"{ev.RecebidoEmUtc.ToLocalTime():HH:mm:ss} • {Name(ev.JogadorId)} • {(ev.PecaJogada is not null ? $"{ev.PecaJogada} ({(ev.Lado == Side.Left ? "esquerda" : "direita")})" : ev.PassouVez ? "passagem" : "evento sem pedra jogada")} • +{ev.PontosConcedidos} pts"));
            var box = new TextBox { Text = string.Join(Environment.NewLine, lines), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20) };
            new Window { Owner = this, Title = "Resultado e histórico confirmado", Width = 850, Height = 550, Content = box, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        });
    }
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (controller?.State is not { } state) return;
        try { Clipboard.SetText(state.PartidaId.ToString()); Notify("ID da partida copiado."); }
        catch { Notify("A área de transferência está em uso. Tente novamente.", true); }
    }
    private string? Prompt(string title, string message, string initial = "")
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 10) });
        var input = new TextBox { Text = initial }; panel.Children.Add(input);
        var buttons = new WrapPanel();
        var ok = new Button { Content = "Confirmar", IsDefault = true }; var cancel = new Button { Content = "Cancelar", IsCancel = true };
        buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons);
        var dialog = new Window { Owner = this, Title = title, Width = 580, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        ok.Click += (_, _) => dialog.DialogResult = true;
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return dialog.ShowDialog() == true ? input.Text : null;
    }
    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        busy = true; UpdateButtons();
        try
        {
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 16, Margin = new Thickness(24),
                Text = "COMO JOGAR\n\nSelecione uma peça destacada e clique em uma ponta iluminada. Os valores que se encostam precisam ser iguais. A abertura usa a peça indicada.\n\nPONTUAÇÃO LOCAL\nA soma dos valores nas duas pontas, quando múltipla de 5, rende soma ÷ 5 pontos: 10 rende 2; 15 rende 3. No modo local atual, cada ponta conta uma vez, inclusive nas duplas.\n\nCOMPRAR E PASSAR\nSem encaixe, compre quando o botão estiver disponível. Se a peça comprada encaixar, você pode jogá-la; se não, a vez muda. Sem peças no monte e sem encaixe, passe.\n\nFIM DA RODADA LOCAL\nA rodada termina quando alguém esvazia a mão ou ocorrem dois passes seguidos. Quem tiver a menor soma nas peças restantes ganha 1 ponto de bônus; empate não dá bônus. Essa convenção local é simplificada e ainda precisa ser comparada com a regra do professor.\n\nDURAÇÃO\nPor pontos: vence quem atingir a meta, mesmo durante uma rodada. Por 3, 5 ou 7 rodadas: vence o maior placar ao fim da última; placares iguais dão empate.\n\nSALVAR E VOLTAR\nA partida contra a CPU é salva automaticamente neste usuário do Windows. Feche e use Continuar minha partida para retomar. Encerrar partida descarta o jogo salvo. Iniciar outra substitui o salvamento anterior.\n\nONLINE\nA API decide regras, ações permitidas e pontuação. O limite de rodadas é uma opção exclusiva da CPU. Sair da sessão não encerra a mesa no servidor." };
            new Window { Owner = this, Title = "Como jogar QUINA", Width = 650, Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new ScrollViewer { Content = text, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } }.ShowDialog();
        }
        finally { busy = false; UpdateButtons(); }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (busy && MessageBox.Show(this, "Há uma requisição em andamento. Fechar agora? A ação pode ter sido aceita; comandos de jogo ficam salvos para repetição.", "Operação em andamento", MessageBoxButton.YesNo) != MessageBoxResult.Yes) { e.Cancel = true; return; }
        if (gateway is DemoGame && !SaveCpu() && MessageBox.Show(this,
            "Não foi possível salvar. Fechar mesmo assim e perder o progresso não salvo?", "Salvar partida",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        { e.Cancel = true; return; }
        timer.Stop(); lifetime.Cancel(); api.Dispose();
    }
}
