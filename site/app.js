/*
 * Microsoft.AI.Local model catalog site.
 *
 * Everything model-specific comes from the provider manifests (eng/catalog/*-models.json), the same files the
 * Microsoft.AI.Local.Catalog.Generators source generator turns into the catalog classes. The task table below mirrors
 * CatalogTask.All in src/Microsoft.AI.Local.Catalog.Generators/CatalogManifest.cs; keep the two in sync when adding a task.
 */
(function () {
  'use strict';

  const REPO_URL = 'https://github.com/andrewleader/converged-ai-apis-foundry-local';
  const DEFAULT_MANIFESTS = ['foundry-models.json', 'windows-models.json'];
  // Deployed site: catalog/ next to index.html (copied by the Pages workflow). Local preview from the repository: ../eng/catalog.
  const CATALOG_BASES = ['catalog/', '../eng/catalog/'];
  const PACKAGE_VERSION = '0.1.0-preview.1';
  const WINDOWS_TFM = 'net8.0-windows10.0.19041.0';
  const PORTABLE_TFM = 'net8.0';

  const TASKS = [
    {
      name: 'text-generation', segment: 'TextGeneration', catalogClass: 'LanguageModels', modelInterface: 'ITextGenerationModel',
      clientType: 'IChatClient', clientNamespace: 'Microsoft.Extensions.AI', text: 'Text generation', category: 'Text', icon: '💬',
      tagline: 'Chat, answer questions, call tools and produce structured output.',
      summary: 'Chat and text completion through Microsoft.Extensions.AI IChatClient: streaming, tool calling and structured output, depending on the model.',
      usage: ['using IChatClient chat = await model.CreateClientAsync();', 'Console.WriteLine(await chat.GetResponseAsync("Why is the sky blue?"));'],
      extraUsings: ['Microsoft.Extensions.AI'],
      lazy: 'model.AsChatClient()', di: 'services.AddLocalChatClient(model);', windowsLaf: true,
    },
    {
      name: 'text-embedding', segment: 'TextEmbedding', catalogClass: 'TextEmbeddingModels', modelInterface: 'ITextEmbeddingModel',
      clientType: 'IEmbeddingGenerator<string, Embedding<float>>', clientNamespace: 'Microsoft.Extensions.AI', text: 'Text embedding', category: 'Text', icon: '🧭',
      tagline: 'Turn text into vectors for search, retrieval-augmented generation and clustering.',
      summary: 'Vector embeddings for search, retrieval-augmented generation and clustering through Microsoft.Extensions.AI IEmbeddingGenerator.',
      usage: ['using var generator = await model.CreateClientAsync();', 'var embeddings = await generator.GenerateAsync(["local AI", "on-device inference"]);'],
      lazy: 'model.AsEmbeddingGenerator()', di: 'services.AddLocalEmbeddingGenerator(model);',
    },
    {
      name: 'speech-to-text', segment: 'SpeechToText', catalogClass: 'SpeechToTextModels', modelInterface: 'ISpeechToTextModel',
      clientType: 'ISpeechToTextClient', clientNamespace: 'Microsoft.Extensions.AI', text: 'Speech to text', category: 'Speech', icon: '🎙️',
      tagline: 'Transcribe audio files and live microphone audio.',
      summary: 'Transcribe audio files and live audio (for example a microphone) through Microsoft.Extensions.AI ISpeechToTextClient. Any audio source works with any model; models that transcribe live return partial results while the user speaks. The API is experimental (MSAILOCAL001) because ISpeechToTextClient is.',
      usage: ['using var client = await model.CreateClientAsync();', 'using var audio = File.OpenRead("meeting.wav");', 'Console.WriteLine((await client.GetTextAsync(audio)).Text);'],
      liveUsage: [
        '// Microphone capture needs a Windows target framework; elsewhere feed a PushAudioStream(AudioFormat.Speech).',
        'await using var microphone = await Microphone.StartAsync();',
        'await foreach (var update in client.GetStreamingTextAsync(microphone))',
        '{',
        '    Console.WriteLine($"{update.Kind}: {update.Text}");   // TextUpdating = partial, TextUpdated = final',
        '}',
        '// microphone.Stop() ends the audio; the loop ends once the last phrase is transcribed.',
      ],
      livePackage: 'Microsoft.AI.Local.Audio',
      pragma: 'MSAILOCAL001', experimental: true,
      providerNotes: {
        Foundry: 'Accepts WAV, MP3, FLAC and Ogg files, headerless 16-bit PCM (set SpeechToTextOptions.SpeechSampleRate) and live audio. Live audio streams into the model as it arrives; models that transcribe live (for example NemotronSpeechStreamingEn_06B) return partial results, others transcribe when the stream ends.',
        Windows: 'Accepts WAV in any sample rate, channel count or encoding, headerless 16-bit PCM (set SpeechToTextOptions.SpeechSampleRate) and live streams (MicrophoneStream, PushAudioStream), with partial results while the user speaks. Compressed formats (MP3, FLAC, Ogg) aren\'t supported.',
      },
      windowsRequirements: [
        'Windows 11, version 24H2 (build 26100) or later. Earlier versions report NotSupportedOnPlatform.',
        'The experimental Windows App SDK (Microsoft.WindowsAppSDK.AI 2.5.4-experimental): deploy the matching experimental runtime or build self-contained. If another reference raises Microsoft.WindowsAppSDK.AI to a stable version, the speech API is missing (build warning MSAILOCAL104, and the model reports MissingAppRequirement).',
        'Runs on the NPU of Copilot+ PCs or on the CPU. On PCs without an NPU, Windows Update downloads the model the first time EnsureReadyAsync runs; ask the user before downloading it.',
        'Microphone capture also needs <DeviceCapability Name="microphone"/> in Package.appxmanifest.',
      ],
      lazy: 'model.AsSpeechToTextClient()', di: 'services.AddLocalSpeechToTextClient(model);',
    },
    {
      name: 'text-summarization', segment: 'TextSummarization', catalogClass: 'TextSummarizationModels', modelInterface: 'ITextSummarizationModel',
      clientType: 'ITextSummarizer', clientNamespace: 'Microsoft.AI.Local', text: 'Text summarization', category: 'Text', icon: '📝',
      tagline: 'Condense long text into a short summary.',
      summary: 'Summarize long text. Any chat model can also summarize through LanguageModels.X.AsTextSummarizationModel().',
      usage: ['using var summarizer = await model.CreateClientAsync();', 'Console.WriteLine((await summarizer.SummarizeAsync(longText)).Text);'],
      adapter: 'LanguageModels.Phi4Mini.AsTextSummarizationModel()', windowsLaf: true,
    },
    {
      name: 'text-rewrite', segment: 'TextRewrite', catalogClass: 'TextRewriteModels', modelInterface: 'ITextRewriteModel',
      clientType: 'ITextRewriter', clientNamespace: 'Microsoft.AI.Local', text: 'Text rewrite', category: 'Text', icon: '✏️',
      tagline: 'Rewrite text to change its tone or improve clarity.',
      summary: 'Rewrite text to change its tone or improve clarity. Any chat model can also rewrite through LanguageModels.X.AsTextRewriteModel().',
      usage: ['using var rewriter = await model.CreateClientAsync();', 'var options = new TextRewriteOptions { Tone = TextRewriteTone.Formal };', 'Console.WriteLine((await rewriter.RewriteAsync(text, options)).Text);'],
      adapter: 'LanguageModels.Phi4Mini.AsTextRewriteModel()', windowsLaf: true,
    },
    {
      name: 'text-to-table', segment: 'TextToTable', catalogClass: 'TextToTableModels', modelInterface: 'ITextToTableModel',
      clientType: 'ITextToTableConverter', clientNamespace: 'Microsoft.AI.Local', text: 'Text to table', category: 'Text', icon: '📊',
      tagline: 'Extract tables from unstructured text.',
      summary: 'Extract tables from unstructured text. Any chat model can also convert through LanguageModels.X.AsTextToTableModel().',
      usage: ['using var converter = await model.CreateClientAsync();', 'var table = await converter.ConvertAsync(text);'],
      adapter: 'LanguageModels.Phi4Mini.AsTextToTableModel()', windowsLaf: true,
    },
    {
      name: 'image-text-recognition', segment: 'ImageTextRecognition', catalogClass: 'ImageTextRecognitionModels', modelInterface: 'ITextRecognitionModel',
      clientType: 'ITextRecognizer', clientNamespace: 'Microsoft.AI.Local', text: 'Text recognition (OCR)', category: 'Image', icon: '🔍',
      tagline: 'Read printed and handwritten text in images.',
      summary: 'Recognize printed and handwritten text in images, with lines, words and bounding boxes.',
      usage: ['using var ocr = await model.CreateClientAsync();', 'var image = await ImageFrame.FromEncodedAsync(File.OpenRead("receipt.png"));', 'foreach (var line in (await ocr.RecognizeAsync(image)).Lines) Console.WriteLine(line.Text);'],
    },
    {
      name: 'image-description', segment: 'ImageDescription', catalogClass: 'ImageDescriptionModels', modelInterface: 'IImageDescriptionModel',
      clientType: 'IImageDescriber', clientNamespace: 'Microsoft.AI.Local', text: 'Image description', category: 'Image', icon: '🖼️',
      tagline: 'Describe what is in an image, including accessible descriptions.',
      summary: 'Describe images (brief, detailed, diagram and accessible descriptions). Vision-capable chat models can also describe images.',
      usage: ['using var describer = await model.CreateClientAsync();', 'var image = await ImageFrame.FromEncodedAsync(File.OpenRead("photo.jpg"));', 'var description = await describer.DescribeAsync(image, new ImageDescriptionOptions { Kind = ImageDescriptionKind.Detailed });'],
      windowsLaf: true,
    },
    {
      name: 'image-scaling', segment: 'ImageScaling', catalogClass: 'ImageScalingModels', modelInterface: 'IImageScalingModel',
      clientType: 'IImageScaler', clientNamespace: 'Microsoft.AI.Local', text: 'Image super-resolution', category: 'Image', icon: '🔎',
      tagline: 'Upscale images while preserving detail.',
      summary: 'Upscale images while preserving detail.',
      usage: ['using var scaler = await model.CreateClientAsync();', 'var image = await ImageFrame.FromEncodedAsync(File.OpenRead("photo.jpg"));', 'var larger = await scaler.ScaleAsync(image, factor: 2.0);'],
    },
    {
      name: 'image-segmentation', segment: 'ImageSegmentation', catalogClass: 'ImageSegmentationModels', modelInterface: 'IImageSegmentationModel',
      clientType: 'IImageSegmenter', clientNamespace: 'Microsoft.AI.Local', text: 'Image segmentation', category: 'Image', icon: '✂️',
      tagline: 'Separate the foreground or specific objects from an image.',
      summary: 'Separate the foreground or specific objects from an image as a mask.',
      usage: ['using var segmenter = await model.CreateClientAsync();', 'var image = await ImageFrame.FromEncodedAsync(File.OpenRead("photo.jpg"));', 'var mask = (await segmenter.SegmentAsync(image)).Mask;'],
    },
    {
      name: 'image-object-removal', segment: 'ImageObjectRemoval', catalogClass: 'ImageObjectRemovalModels', modelInterface: 'IObjectRemovalModel',
      clientType: 'IImageObjectRemover', clientNamespace: 'Microsoft.AI.Local', text: 'Object removal', category: 'Image', icon: '🧽',
      tagline: 'Erase objects from an image and fill in the background.',
      summary: 'Erase the objects under a mask and fill in the background.',
      usage: ['using var remover = await model.CreateClientAsync();', 'var edited = await remover.RemoveAsync(image, mask);'],
    },
  ];
  const TASK_BY_NAME = new Map(TASKS.map(t => [t.name, t]));

  const PLATFORMS = [
    { rid: 'win-x64', os: 'Windows', arch: 'x64' },
    { rid: 'win-arm64', os: 'Windows', arch: 'Arm64' },
    { rid: 'osx-arm64', os: 'macOS', arch: 'Apple silicon' },
    { rid: 'osx-x64', os: 'macOS', arch: 'Intel' },
    { rid: 'linux-x64', os: 'Linux', arch: 'x64' },
    { rid: 'linux-arm64', os: 'Linux', arch: 'Arm64' },
  ];
  const PLATFORM_BY_RID = new Map(PLATFORMS.map(p => [p.rid, p]));
  const OSES = ['Windows', 'macOS', 'Linux'];

  const CAPABILITIES = [
    { key: 'streaming', label: 'Streaming' },
    { key: 'toolCalling', label: 'Tool calling' },
    { key: 'structuredOutput', label: 'Structured output' },
    { key: 'reasoning', label: 'Reasoning' },
    { key: 'imageInput', label: 'Image input' },
    { key: 'audioInput', label: 'Audio input' },
  ];

  const CONTEXT_OPTIONS = [0, 8192, 16384, 32768, 131072];

  const PROVIDER_INFO = {
    Foundry: {
        blurb: 'Downloaded on first use and run in-process. One net8.0 package for Windows, macOS and Linux.',
      requirements: [
        'Target net8.0 or later; the same package runs on Windows, macOS (Apple silicon) and Linux.',
        'The model downloads on first use (EnsureReadyAsync reports progress); make sure the device has the disk space and memory for it.',
        'Optional: pin a device with WithDevice(LocalDevice.Gpu), and configure the cache directory and execution providers with FoundryProvider.Configure (Microsoft.AI.Local.Foundry).',
      ],
    },
    Windows: {
        blurb: 'Built into Windows on Copilot+ PCs. Windows delivers and services the model; the app carries no model files or ONNX Runtime.',
      requirements: [
        `Target a Windows TFM (for example ${WINDOWS_TFM}). A plain net8.0 app gets the portable build, where every handle reports NotSupportedOnPlatform (analyzer MSAILOCAL101).`,
        'Package identity and the systemAIModels capability in Package.appxmanifest (analyzers MSAILOCAL102 and MSAILOCAL103).',
      ],
      defaultTaskRequirements: ['A Copilot+ PC with an NPU.'],
      lafRequirement: 'This model runs on Phi Silica, a Limited Access Feature: set the WindowsAILimitedAccessFeatureToken and WindowsAILimitedAccessFeatureAttestation MSBuild properties (see Microsoft.AI.Local.Windows).',
    },
  };

  // ---------- helpers ----------

  function h(tag, attrs, ...children) {
    const el = document.createElement(tag);
    if (attrs) {
      for (const [k, v] of Object.entries(attrs)) {
        if (v === undefined || v === null || v === false) continue;
        if (k === 'class') el.className = v;
        else if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2), v);
        else if (k === 'checked' || k === 'value' || k === 'selected') el[k] = v;
        else el.setAttribute(k, v === true ? '' : String(v));
      }
    }
    append(el, children);
    return el;
  }

  function append(el, children) {
    for (const c of children) {
      if (c === undefined || c === null || c === false) continue;
      if (Array.isArray(c)) append(el, c);
      else el.appendChild(c instanceof Node ? c : document.createTextNode(String(c)));
    }
  }

  function formatTokens(n) {
    if (!n) return '';
    if (n >= 1024 && n % 1024 === 0) return `${n / 1024}K`;
    return n.toLocaleString('en-US');
  }

  function modelHref(m) {
    return `#/task/${encodeURIComponent(m.task.name)}/${encodeURIComponent(m.property)}`;
  }

  function osList(m) {
    return OSES.filter(os => m.platforms.some(rid => PLATFORM_BY_RID.get(rid) && PLATFORM_BY_RID.get(rid).os === os));
  }

  function copyButton(getText) {
    const btn = h('button', { type: 'button', class: 'copy-btn', 'aria-label': 'Copy to clipboard' }, 'Copy');
    btn.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(getText());
        btn.textContent = 'Copied';
      } catch {
        btn.textContent = 'Press Ctrl+C';
      }
      setTimeout(() => { btn.textContent = 'Copy'; }, 1500);
    });
    return btn;
  }

  function codeBlock(code, lang) {
    return h('div', { class: 'code' },
      h('div', { class: 'code-bar' }, h('span', { class: 'code-lang' }, lang), copyButton(() => code)),
      h('pre', null, h('code', null, code)));
  }

  function tabs(items) {
    const id = 'tabs-' + Math.random().toString(36).slice(2, 8);
    const buttons = [];
    const panels = [];
    items.forEach((item, i) => {
      const btn = h('button', {
        type: 'button', role: 'tab', id: `${id}-t${i}`, 'aria-controls': `${id}-p${i}`,
        'aria-selected': i === 0 ? 'true' : 'false', tabindex: i === 0 ? '0' : '-1', class: 'tab',
      }, item.label);
      const panel = h('div', { role: 'tabpanel', id: `${id}-p${i}`, 'aria-labelledby': `${id}-t${i}`, hidden: i !== 0 }, item.content);
      btn.addEventListener('click', () => select(i));
      btn.addEventListener('keydown', e => {
        if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
          const next = (i + (e.key === 'ArrowRight' ? 1 : items.length - 1)) % items.length;
          select(next);
          buttons[next].focus();
        }
      });
      buttons.push(btn);
      panels.push(panel);
    });
    function select(i) {
      buttons.forEach((b, j) => { b.setAttribute('aria-selected', j === i ? 'true' : 'false'); b.tabIndex = j === i ? 0 : -1; });
      panels.forEach((p, j) => { p.hidden = j !== i; });
    }
    return h('div', { class: 'tabs' }, h('div', { role: 'tablist', class: 'tablist' }, buttons), panels);
  }

  function badge(text, kind, title) {
    return h('span', { class: `badge ${kind || ''}`, title }, text);
  }

  // ---------- data ----------
  // The catalog is converged: models from every source are presented together, grouped by task. The source a model
  // comes from (m.provider) is only used internally to pick its package, requirements and code snippets.

  async function fetchJson(path) {
    const res = await fetch(path, { cache: 'no-cache' });
    if (!res.ok) throw new Error(`${path}: HTTP ${res.status}`);
    return res.json();
  }

  async function loadCatalog() {
    let lastError;
    for (const base of CATALOG_BASES) {
      try {
        let files = DEFAULT_MANIFESTS;
        try {
          const index = await fetchJson(base + 'index.json');
          if (Array.isArray(index) && index.length) files = index;
        } catch { /* no index: use the default manifest list */ }
        const manifests = await Promise.all(files.map(f => fetchJson(base + f)));
        return normalize(manifests);
      } catch (e) {
        lastError = e;
      }
    }
    throw lastError || new Error('No catalog found.');
  }

  function normalize(manifests) {
    const models = [];
    for (const manifest of manifests) {
      const p = manifest.provider;
      for (const raw of manifest.models || []) {
        const task = TASK_BY_NAME.get(raw.task);
        if (!task) continue;
        const caps = raw.capabilities || {};
        models.push({
          order: models.length,
          providerPrefix: p.idPrefix,
          provider: p.name,
          providerNamespace: p.namespace,
          providerRemarks: p.remarks,
          alias: raw.alias,
          property: raw.property,
          displayName: raw.displayName || raw.alias,
          publisher: raw.publisher || '',
          description: raw.description || '',
          task,
          platforms: raw.platforms || [],
          crossPlatform: raw.crossPlatform === true,
          capabilities: {
            streaming: caps.streaming === true,
            toolCalling: caps.toolCalling === true,
            structuredOutput: caps.structuredOutput === true,
            reasoning: caps.reasoning === true,
            imageInput: caps.imageInput === true,
            audioInput: caps.audioInput === true,
          },
          contextLength: typeof caps.contextLength === 'number' ? caps.contextLength : null,
          maxOutputTokens: typeof caps.maxOutputTokens === 'number' ? caps.maxOutputTokens : null,
          retired: raw.retired || null,
          handle: `${task.catalogClass}.${raw.property}`,
          package: `Microsoft.AI.Local.${task.segment}.${p.name}`,
          taskPackage: `Microsoft.AI.Local.${task.segment}`,
        });
      }
    }
    for (const m of models) {
      m.searchText = [m.displayName, m.alias, m.property, m.handle, m.publisher, m.description,
        m.task.text, m.task.name, m.package, ...m.platforms, ...osList(m)].join(' ').toLowerCase();
    }
    return { models };
  }

  // ---------- snippets ----------

  function installCli(packages) {
    return packages.map(p => `dotnet add package ${p} --prerelease`).join('\n');
  }

  function packageReferences(packages) {
    return packages.map(p => `<PackageReference Include="${p}" Version="${PACKAGE_VERSION}" />`).join('\n');
  }

  function projectFile(m) {
    const windows = m.provider === 'Windows';
    const lines = [
      '<Project Sdk="Microsoft.NET.Sdk">',
      '  <PropertyGroup>',
      '    <OutputType>Exe</OutputType>',
      `    <TargetFramework>${windows ? WINDOWS_TFM : PORTABLE_TFM}</TargetFramework>`,
    ];
    if (windows && m.task.windowsLaf) {
      lines.push(
        '    <!-- Phi Silica Limited Access Feature: keep the token out of source control. -->',
        '    <WindowsAILimitedAccessFeatureToken>$(PHI_SILICA_LAF_TOKEN)</WindowsAILimitedAccessFeatureToken>',
        '    <WindowsAILimitedAccessFeatureAttestation>CONTOSO has registered their use of com.microsoft.windows.ai.languagemodel with Microsoft and agrees to the terms of use.</WindowsAILimitedAccessFeatureAttestation>');
    }
    lines.push('  </PropertyGroup>', '', '  <ItemGroup>', `    <PackageReference Include="${m.package}" Version="${PACKAGE_VERSION}" />`, '  </ItemGroup>', '</Project>');
    return lines.join('\n');
  }

  function usings(task) {
    const list = ['Microsoft.AI.Local', ...(task.extraUsings || [])];
    return list.map(u => `using ${u};`).join('\n');
  }

  function quickStart(m) {
    const t = m.task;
    const lines = [];
    if (t.pragma) lines.push(`#pragma warning disable ${t.pragma} // experimental API`);
    lines.push(usings(t), '', `${t.modelInterface} model = ${m.handle};`, '');
    if (m.provider === 'Foundry') {
      lines.push('// Downloads the model on first use and loads it. Idempotent and coalesced.',
        'await model.EnsureReadyAsync(new Progress<ModelAcquisitionProgress>(p =>',
        '    Console.Write($"\\r{p.Stage,-20} {p.OverallFraction:P0}")));');
    } else {
      lines.push('// Windows delivers the model; this makes sure it is installed and ready.', 'await model.EnsureReadyAsync();');
    }
    lines.push(...t.usage);
    return lines.join('\n');
  }

  function availabilitySnippet(m) {
    const t = m.task;
    const lines = [];
    if (t.pragma) lines.push(`#pragma warning disable ${t.pragma}`);
    lines.push(usings(t), '', `${t.modelInterface} model = ${m.handle};`, '',
      'var availability = await model.GetAvailabilityAsync();',
      'if (!availability.IsAvailable)',
      '{',
      '    // For example NotSupportedOnPlatform or MissingAppRequirement, with an actionable reason.',
      '    Console.WriteLine($"{availability.Status}: {availability.Reason}");',
      '    return;',
      '}');
    return lines.join('\n');
  }

  function fallbackSnippet(m, others) {
    const t = m.task;
    const handles = [m, ...others].map(x => x.handle);
    const lines = [];
    if (t.pragma) lines.push(`#pragma warning disable ${t.pragma}`);
    lines.push(usings(t), '', '// Picks the first model that can run here (for example, the model built into Windows on a Copilot+ PC,',
      '// otherwise a cross-platform model that downloads on first use).',
      `${t.modelInterface} model = await LocalModel.SelectFirstAvailableAsync(`);
    handles.forEach((hdl, i) => lines.push(`    ${hdl}${i === handles.length - 1 ? ');' : ','}`));
    lines.push('', 'await model.EnsureReadyAsync();', ...t.usage);
    return lines.join('\n');
  }

  function liveSnippet(m) {
    const t = m.task;
    const lines = [];
    if (t.pragma) lines.push(`#pragma warning disable ${t.pragma}`);
    lines.push(usings(t), '', `// Live audio sources come from ${t.livePackage}; they work with every model of this task.`,
      `${t.modelInterface} model = ${m.handle};`, '', 'await model.EnsureReadyAsync();', 'using var client = await model.CreateClientAsync();', '', ...t.liveUsage);
    return lines.join('\n');
  }

  function diSnippet(m) {
    const t = m.task;
    const lines = [];
    if (t.pragma) lines.push(`#pragma warning disable ${t.pragma}`);
    lines.push(usings(t), 'using Microsoft.Extensions.DependencyInjection;', '', `${t.modelInterface} model = ${m.handle};`, '',
      '// Registers the client; the model is acquired lazily on first use.', t.di, '', `// Or, without DI: ${t.lazy}`);
    return lines.join('\n');
  }

  // ---------- state ----------

  let catalog = { models: [] };
  const app = document.getElementById('app');

  // Query-string filters. The home page uses q, os and platform (one value each); a task page uses all of them.
  const FILTER_KEYS = ['q', 'os', 'platform', 'publisher', 'cap', 'ctx', 'xplat', 'retired', 'sort', 'view'];

  function parseFilters(query) {
    const params = new URLSearchParams(query);
    const list = k => (params.get(k) || '').split(',').filter(Boolean);
    return {
      q: params.get('q') || '',
      os: list('os').filter(os => OSES.includes(os)),
      platform: list('platform').filter(rid => PLATFORM_BY_RID.has(rid)),
      publisher: list('publisher'),
      cap: list('cap'),
      ctx: Number(params.get('ctx')) || 0,
      xplat: params.get('xplat') === '1',
      retired: params.get('retired') === '1',
      sort: params.get('sort') || 'featured',
      view: params.get('view') || 'grid',
    };
  }

  function serializeFilters(f, keys) {
    const params = new URLSearchParams();
    for (const k of keys || FILTER_KEYS) {
      const v = f[k];
      if (Array.isArray(v)) { if (v.length) params.set(k, v.join(',')); }
      else if (typeof v === 'boolean') { if (v) params.set(k, '1'); }
      else if (k === 'sort') { if (v !== 'featured') params.set(k, v); }
      else if (k === 'view') { if (v !== 'grid') params.set(k, v); }
      else if (v) params.set(k, String(v));
    }
    const s = params.toString();
    return s ? `?${s}` : '';
  }

  function matchesPlatform(m, f) {
    return f.platform.every(rid => m.platforms.includes(rid)) && f.os.every(os => osList(m).includes(os));
  }

  function matchesSearch(m, q) {
    const terms = q.toLowerCase().split(/\s+/).filter(Boolean);
    return terms.every(t => m.searchText.includes(t));
  }

  function applyFilters(models, f) {
    return models.filter(m =>
      (f.retired || !m.retired) &&
      matchesSearch(m, f.q) &&
      matchesPlatform(m, f) &&
      (!f.publisher.length || f.publisher.includes(m.publisher)) &&
      f.cap.every(c => m.capabilities[c]) &&
      (!f.ctx || (m.contextLength || 0) >= f.ctx) &&
      (!f.xplat || m.crossPlatform));
  }

  const byName = (a, b) => a.displayName.localeCompare(b.displayName, 'en', { numeric: true });
  const SORTS = {
    featured: { label: 'Featured', cmp: (a, b) => a.order - b.order },
    name: { label: 'Name', cmp: byName },
    platforms: { label: 'Platform coverage', cmp: (a, b) => b.platforms.length - a.platforms.length || Number(b.crossPlatform) - Number(a.crossPlatform) || a.order - b.order },
    context: { label: 'Context length', cmp: (a, b) => (b.contextLength || 0) - (a.contextLength || 0) || a.order - b.order, when: models => models.some(m => m.contextLength) },
  };

  function taskModels(t) {
    return catalog.models.filter(m => m.task === t);
  }

  function platformLabel(f) {
    const parts = [...f.os, ...f.platform];
    return parts.length ? parts.join(', ') : '';
  }

  // ---------- views ----------

  function setTitle(text) {
    document.title = text ? `${text} · Microsoft.AI.Local models` : 'Microsoft.AI.Local model catalog';
  }

  function setNav(name) {
    document.querySelectorAll('[data-nav]').forEach(a => {
      if (a.dataset.nav === name) a.setAttribute('aria-current', 'page'); else a.removeAttribute('aria-current');
    });
  }

  function platformChips(m) {
    return h('span', { class: 'platform-chips' }, osList(m).map(os => {
      const rids = m.platforms.filter(r => PLATFORM_BY_RID.get(r).os === os).map(r => PLATFORM_BY_RID.get(r).arch);
      return badge(os, `os os-${os.toLowerCase()}`, `${os}: ${rids.join(', ')}`);
    }));
  }

  function capabilityChips(m) {
    const chips = CAPABILITIES.filter(c => m.capabilities[c.key]).map(c => badge(c.label, 'cap'));
    if (m.contextLength) chips.push(badge(`${formatTokens(m.contextLength)} context`, 'ctx', `${m.contextLength.toLocaleString('en-US')} tokens`));
    return chips;
  }

  function modelCard(m, showTask) {
    return h('article', { class: `card model-card${m.retired ? ' retired' : ''}` },
      showTask ? h('div', { class: 'card-top' }, h('a', { class: 'task-tag', href: taskHref(m.task) }, `${m.task.icon} ${m.task.text}`)) : null,
      h('h3', null, h('a', { href: modelHref(m) }, m.displayName)),
      h('p', { class: 'meta' }, m.publisher ? `${m.publisher} · ` : '', h('code', null, m.handle)),
      h('p', { class: 'desc' }, m.description),
      h('div', { class: 'chips' },
        platformChips(m),
        m.crossPlatform ? badge('✓ Cross-platform validated', 'xplat', 'Validated cross-platform by the conformance suite') : null,
        m.retired ? badge('Retired', 'retired') : null),
      h('div', { class: 'chips' }, capabilityChips(m)));
  }

  function modelTable(models, showTask) {
    const showCtx = models.some(m => m.contextLength);
    const showCaps = models.some(m => CAPABILITIES.some(c => m.capabilities[c.key]));
    return h('div', { class: 'table-wrap' }, h('table', { class: 'models-table' },
      h('thead', null, h('tr', null,
        h('th', { scope: 'col' }, 'Model'),
        showTask ? h('th', { scope: 'col' }, 'Task') : null,
        h('th', { scope: 'col' }, 'Platforms'),
        showCaps ? h('th', { scope: 'col' }, 'Capabilities') : null,
        showCtx ? h('th', { scope: 'col' }, 'Context') : null)),
      h('tbody', null, models.map(m => h('tr', { class: m.retired ? 'retired' : null },
        h('td', null, h('a', { href: modelHref(m) }, m.displayName), m.retired ? [' ', badge('Retired', 'retired')] : null,
          h('div', { class: 'muted small' }, h('code', null, m.handle))),
        showTask ? h('td', null, h('a', { href: taskHref(m.task) }, m.task.text)) : null,
        h('td', null, platformChips(m), m.crossPlatform ? h('div', { class: 'small xplat-text' }, '✓ cross-platform validated') : null),
        showCaps ? h('td', null, CAPABILITIES.filter(c => m.capabilities[c.key]).map(c => c.label).join(', ') || '—') : null,
        showCtx ? h('td', { class: 'num' }, m.contextLength ? formatTokens(m.contextLength) : '—') : null)))));
  }

  function checkboxGroup(title, key, options, f, rerender, hint) {
    return h('fieldset', { class: 'filter-group' },
      h('legend', null, title, hint ? h('span', { class: 'hint' }, ` ${hint}`) : null),
      options.map(o => h('label', { class: 'check' },
        h('input', {
          type: 'checkbox', value: o.value, checked: f[key].includes(o.value),
          onchange: e => {
            f[key] = e.target.checked ? [...f[key], o.value] : f[key].filter(v => v !== o.value);
            rerender();
          },
        }),
        h('span', null, o.label),
        o.count !== undefined ? h('span', { class: 'count' }, o.count) : null)));
  }

  function taskHref(t, f) {
    return `#/task/${t.name}${f ? serializeFilters(f, ['os', 'platform']) : ''}`;
  }

  function keepSearchFocus(search, hadFocus) {
    if (hadFocus) { search.focus(); search.setSelectionRange(search.value.length, search.value.length); }
  }

  // Home: every task, grouped by category, narrowed to the platform the user picks.
  function renderHome(query) {
    setNav('tasks');
    setTitle('');
    const f = parseFilters(query);
    // The home page picks one OS and at most one architecture.
    f.os = f.os.slice(0, 1);
    f.platform = f.platform.filter(rid => !f.os.length || PLATFORM_BY_RID.get(rid).os === f.os[0]).slice(0, 1);
    const all = catalog.models.filter(m => !m.retired);
    const tasksWithModels = TASKS.filter(t => all.some(m => m.task === t));

    function navigate(replaceOnly) {
      const hash = `#/${serializeFilters(f, ['q', 'os', 'platform'])}`;
      if (location.hash !== hash) history.replaceState(null, '', hash);
      if (!replaceOnly) renderHome(hash.slice(2).replace(/^\?/, ''));
    }

    let resultsEl;
    function renderResults() {
      const onPlatform = all.filter(m => matchesPlatform(m, f));
      const q = f.q.trim().toLowerCase();
      const taskMatches = t => !q || q.split(/\s+/).every(term =>
        [t.text, t.name, t.category, t.tagline, t.summary, t.clientType, t.catalogClass].join(' ').toLowerCase().includes(term));
      const matchingModels = q ? onPlatform.filter(m => matchesSearch(m, f.q)) : [];
      const visible = tasksWithModels.filter(t => onPlatform.some(m => m.task === t) &&
        (taskMatches(t) || matchingModels.some(m => m.task === t)));
      const hiddenByPlatform = tasksWithModels.filter(t => !onPlatform.some(m => m.task === t));
      const label = platformLabel(f);

      resultsEl.replaceChildren();
      resultsEl.append(h('p', { class: 'summary', 'aria-live': 'polite' },
        `${visible.length} task${visible.length === 1 ? '' : 's'}`, label ? ` with a model that runs on ${label}` : ''));

      if (!visible.length) {
        resultsEl.append(h('div', { class: 'empty' }, h('p', null, 'No tasks match.'),
          h('button', { type: 'button', class: 'btn', onclick: () => { location.hash = '#/'; } }, 'Clear filters')));
      }
      for (const cat of [...new Set(TASKS.map(t => t.category))]) {
        const tasks = visible.filter(t => t.category === cat);
        if (!tasks.length) continue;
        resultsEl.append(h('section', { class: 'task-group' },
          h('h2', null, cat),
          h('div', { class: 'grid task-grid' }, tasks.map(t => {
            const models = onPlatform.filter(m => m.task === t);
            const oses = OSES.filter(os => all.some(m => m.task === t && osList(m).includes(os)));
            const href = taskHref(t, f);
            return h('a', { class: 'card task-card', href },
              h('div', { class: 'task-card-head' },
                h('span', { class: 'task-icon', 'aria-hidden': 'true' }, t.icon),
                h('h3', null, t.text)),
              h('p', { class: 'desc' }, t.tagline),
              h('div', { class: 'chips' },
                badge(`${models.length} model${models.length === 1 ? '' : 's'}`, 'count-badge'),
                oses.map(os => badge(os, `os os-${os.toLowerCase()}`)),
                t.experimental ? badge('Experimental API', 'experimental') : null));
          }))));
      }
      if (hiddenByPlatform.length && label) {
        resultsEl.append(h('p', { class: 'small muted hidden-note' },
          `Not available on ${label} yet: `, hiddenByPlatform.map((t, i) => [i ? ', ' : '', h('a', { href: taskHref(t) }, t.text)]), '.'));
      }
      if (matchingModels.length) {
        resultsEl.append(h('section', { class: 'task-group' },
          h('h2', null, 'Matching models', h('span', { class: 'count' }, matchingModels.length)),
          modelTable(matchingModels, true)));
      }
    }

    const search = h('input', {
      type: 'search', class: 'search', placeholder: 'Search tasks and models, for example "speech" or "phi"…', value: f.q,
      'aria-label': 'Search tasks and models',
      oninput: e => { f.q = e.target.value; navigate(true); renderResults(); },
    });

    const osPills = h('div', { class: 'pills', role: 'group', 'aria-label': 'Operating system' },
      [['', 'All platforms'], ...OSES.map(os => [os, os])].map(([os, label]) =>
        h('button', {
          type: 'button', class: 'pill', 'aria-pressed': (f.os[0] || '') === os ? 'true' : 'false',
          onclick: () => { f.os = os ? [os] : []; f.platform = []; navigate(); },
        }, label)));

    const archOptions = PLATFORMS.filter(p => f.os.length && p.os === f.os[0] && all.some(m => m.platforms.includes(p.rid)));
    const archSelect = archOptions.length > 1 ? h('label', { class: 'inline' }, 'Architecture ',
      h('select', { onchange: e => { f.platform = e.target.value ? [e.target.value] : []; navigate(); } },
        h('option', { value: '' }, 'Any'),
        archOptions.map(p => h('option', { value: p.rid, selected: f.platform[0] === p.rid }, `${p.arch} (${p.rid})`)))) : null;

    resultsEl = h('div', { class: 'results' });
    const hadFocus = document.activeElement && document.activeElement.classList.contains('search');
    app.replaceChildren(
      h('section', { class: 'hero' },
        h('h1', null, 'Ready-to-use local AI models from Microsoft'),
        h('p', { class: 'lead' }, `${all.length} models for ${tasksWithModels.length} tasks that run on the user's device, behind one set of `,
          h('code', null, 'Microsoft.AI.Local'), ' APIs. Pick a task, then a model that runs where your app runs.'),
        search,
        h('div', { class: 'filter-bar' }, h('span', { class: 'filter-label' }, 'Runs on'), osPills, archSelect)),
      resultsEl);
    renderResults();
    keepSearchFocus(search, hadFocus);
  }

  function taskSnippets(t, models) {
    const live = models.filter(m => !m.retired);
    const first = live[0];
    const snippetTabs = [];
    if (first) snippetTabs.push({ label: 'Quick start', content: codeBlock(quickStart(first), 'C#') });
    const fallback = fallbackChain(live);
    if (fallback.length > 1) snippetTabs.push({ label: 'With fallback', content: codeBlock(fallbackSnippet(fallback[0], fallback.slice(1)), 'C#') });
    if (first && t.liveUsage) snippetTabs.push({ label: 'Live audio', content: codeBlock(liveSnippet(first), 'C#') });
    if (first && t.di) snippetTabs.push({ label: 'Dependency injection', content: codeBlock(diSnippet(first), 'C#') });
    if (t.adapter) {
      snippetTabs.push({
        label: 'Any chat model', content: codeBlock([
          usings(t), '', '// Requires the package of the chat model you use (here Microsoft.AI.Local.TextGeneration.Foundry).',
          `${t.modelInterface} model = ${t.adapter};`, '', 'await model.EnsureReadyAsync();', ...t.usage].join('\n'), 'C#'),
      });
    }
    return snippetTabs;
  }

  // A model built into Windows first (no download), then the most portable model that runs elsewhere.
  function fallbackChain(models) {
    const chain = [];
    for (const group of [...new Set(models.map(m => m.provider))]) {
      const candidates = models.filter(m => m.provider === group);
      chain.push(candidates.find(m => m.crossPlatform) || candidates[0]);
    }
    return chain.sort((a, b) => a.platforms.length - b.platforms.length);
  }

  function renderTask(name, query) {
    const t = TASK_BY_NAME.get(name);
    if (!t) return renderNotFound();
    setNav('tasks');
    setTitle(t.text);
    const f = parseFilters(query);
    const all = taskModels(t);
    const live = all.filter(m => !m.retired);
    const count = pred => all.filter(m => (f.retired || !m.retired) && pred(m)).length;
    const publishers = [...new Set(all.map(m => m.publisher).filter(Boolean))].sort();
    const caps = CAPABILITIES.filter(c => all.some(m => m.capabilities[c.key]));
    const hasCtx = all.some(m => m.contextLength);
    const sorts = Object.entries(SORTS).filter(([, s]) => !s.when || s.when(all));
    if (!SORTS[f.sort]) f.sort = 'featured';

    let resultsEl;
    let summaryEl;
    function update(full) {
      const hash = `#/task/${t.name}${serializeFilters(f)}`;
      if (location.hash !== hash) history.replaceState(null, '', hash);
      if (full) renderTask(name, serializeFilters(f).slice(1)); else renderResults();
    }
    const rerender = () => update(true);

    function renderResults() {
      const filtered = applyFilters(all, f).sort(SORTS[f.sort].cmp);
      const total = all.filter(m => f.retired || !m.retired).length;
      summaryEl.textContent = `${filtered.length} of ${total} model${total === 1 ? '' : 's'}`;
      resultsEl.replaceChildren();
      if (!filtered.length) {
        resultsEl.append(h('div', { class: 'empty' }, h('p', null, 'No models match these filters.'),
          h('button', { type: 'button', class: 'btn', onclick: () => { location.hash = taskHref(t); } }, 'Clear filters')));
      } else if (f.view === 'table') {
        resultsEl.append(modelTable(filtered, false));
      } else {
        resultsEl.append(h('div', { class: 'grid' }, filtered.map(m => modelCard(m, false))));
      }
    }

    const search = h('input', {
      type: 'search', class: 'search small-search', placeholder: `Search ${t.text.toLowerCase()} models…`, value: f.q,
      'aria-label': `Search ${t.text.toLowerCase()} models`,
      oninput: e => { f.q = e.target.value; update(false); },
    });

    const activeCount = f.platform.length + f.os.length + f.publisher.length + f.cap.length +
      (f.ctx ? 1 : 0) + (f.xplat ? 1 : 0) + (f.retired ? 1 : 0);

    const sidebar = h('aside', { class: 'filters', 'aria-label': 'Filters' },
      h('div', { class: 'filters-head' }, h('h2', null, 'Filters'),
        activeCount ? h('a', { href: taskHref(t), class: 'small' }, `Clear (${activeCount})`) : null),
      checkboxGroup('Operating system', 'os', OSES.map(os => ({ value: os, label: os, count: count(m => osList(m).includes(os)) }))
        .filter(o => o.count > 0 || f.os.includes(o.value)), f, rerender, '(all selected)'),
      checkboxGroup('Platform (RID)', 'platform', PLATFORMS.map(p => ({ value: p.rid, label: p.rid, count: count(m => m.platforms.includes(p.rid)) }))
        .filter(o => o.count > 0 || f.platform.includes(o.value)), f, rerender, '(all selected)'),
      all.some(m => m.crossPlatform) ? h('fieldset', { class: 'filter-group' }, h('legend', null, 'Portability'),
        h('label', { class: 'check' },
          h('input', { type: 'checkbox', checked: f.xplat, onchange: e => { f.xplat = e.target.checked; rerender(); } }),
          h('span', null, 'Cross-platform validated'), h('span', { class: 'count' }, count(m => m.crossPlatform)))) : null,
      caps.length ? checkboxGroup('Capabilities', 'cap', caps.map(c => ({ value: c.key, label: c.label, count: count(m => m.capabilities[c.key]) })), f, rerender, '(all selected)') : null,
      hasCtx ? h('fieldset', { class: 'filter-group' }, h('legend', null, 'Minimum context length'),
        h('select', {
          'aria-label': 'Minimum context length',
          onchange: e => { f.ctx = Number(e.target.value); rerender(); },
        }, CONTEXT_OPTIONS.map(n => h('option', { value: String(n), selected: f.ctx === n }, n ? `${formatTokens(n)} tokens or more` : 'Any')))) : null,
      publishers.length > 1 ? checkboxGroup('Publisher', 'publisher', publishers.map(p => ({ value: p, label: p, count: count(m => m.publisher === p) })), f, rerender) : null,
      all.some(m => m.retired) ? h('fieldset', { class: 'filter-group' }, h('legend', null, 'Lifecycle'),
        h('label', { class: 'check' },
          h('input', { type: 'checkbox', checked: f.retired, onchange: e => { f.retired = e.target.checked; rerender(); } }),
          h('span', null, 'Include retired models'))) : null);

    summaryEl = h('p', { class: 'summary', 'aria-live': 'polite' });
    resultsEl = h('div', { class: 'results' });

    const toolbar = h('div', { class: 'toolbar' },
      search,
      summaryEl,
      sorts.length > 1 ? h('label', { class: 'inline' }, 'Sort ',
        h('select', { onchange: e => { f.sort = e.target.value; update(false); } },
          sorts.map(([k, s]) => h('option', { value: k, selected: f.sort === k }, s.label)))) : null,
      h('div', { class: 'seg', role: 'group', 'aria-label': 'Layout' },
        [['grid', 'Cards'], ['table', 'Table']].map(([k, label]) =>
          h('button', { type: 'button', class: 'seg-btn', 'aria-pressed': f.view === k ? 'true' : 'false', onclick: () => { f.view = k; rerender(); } }, label))));

    const packages = [...new Set(live.map(m => m.package))];
    const snippetTabs = taskSnippets(t, all);
    const groups = [...new Set(live.map(m => m.provider))];

    const hadFocus = document.activeElement && document.activeElement.classList.contains('search');
    app.replaceChildren(
      h('nav', { class: 'crumbs', 'aria-label': 'Breadcrumb' }, h('a', { href: '#/' }, 'Tasks'), ' / ', t.text),
      h('section', { class: 'hero' },
        h('div', { class: 'chips' }, h('span', { class: 'task-tag' }, t.category),
          t.experimental ? badge('Experimental API', 'experimental', `Suppress ${t.pragma} to use it`) : null),
        h('h1', null, `${t.icon} ${t.text}`),
        h('p', { class: 'lead' }, t.summary)),
      h('h2', { class: 'section-title' }, 'Models'),
      h('div', { class: 'layout' }, sidebar, h('div', { class: 'main-col' }, toolbar, resultsEl)),
      h('h2', { class: 'section-title' }, 'Use it in your app'),
      h('div', { class: 'detail-grid' },
        h('div', null,
          h('h3', null, 'API'),
          h('table', { class: 'kv' }, h('tbody', null,
            h('tr', null, h('th', { scope: 'row' }, 'Catalog class'), h('td', null, h('code', null, t.catalogClass))),
            h('tr', null, h('th', { scope: 'row' }, 'Model interface'), h('td', null, h('code', null, t.modelInterface))),
            h('tr', null, h('th', { scope: 'row' }, 'Client'), h('td', null, h('code', null, t.clientType), h('span', { class: 'muted small' }, ` (${t.clientNamespace})`))),
            h('tr', null, h('th', { scope: 'row' }, 'Task package'), h('td', null, h('code', null, `Microsoft.AI.Local.${t.segment}`))))),
          h('p', { class: 'small muted' }, 'Code written against the task works with every model of the task; switching models changes one line. ',
            h('a', { href: `${REPO_URL}/tree/main/src/Microsoft.AI.Local.${t.segment}` }, 'Task package source'), '.'),
          h('h3', null, 'Install'),
          h('p', { class: 'small muted' }, 'Each model page names the package that provides it. To use every model of this task:'),
          tabs([
            { label: '.NET CLI', content: codeBlock(installCli(packages), 'shell') },
            { label: 'PackageReference', content: codeBlock(packageReferences(packages), 'XML') },
          ]),
          t.livePackage ? h('p', { class: 'small muted' }, 'For live audio (microphone capture or a PushAudioStream), also add ',
            h('code', null, t.livePackage), '.') : null,
          t.providerNotes ? [h('h3', null, 'Audio input'), h('ul', { class: 'req-list' },
            groups.filter(g => t.providerNotes[g]).map(g => h('li', null,
              h('strong', null, `${live.filter(m => m.provider === g).map(m => m.displayName).join(', ')}: `), t.providerNotes[g])))] : null),
        h('div', null, h('h3', null, 'Code'), snippetTabs.length ? tabs(snippetTabs) : h('p', null, 'No models yet.'))));
    renderResults();
    keepSearchFocus(search, hadFocus);
  }

  function renderModel(m) {
    if (!m) return renderNotFound();
    setNav('tasks');
    setTitle(m.displayName);
    const t = m.task;
    const pinfo = PROVIDER_INFO[m.provider];
    const others = catalog.models.filter(x => x.task === t && x !== m && !x.retired);
    const fallbackPartners = fallbackChain(others.filter(x => x.provider !== m.provider));

    const requirements = [];
    if (pinfo) {
      requirements.push(...pinfo.requirements);
      if (m.provider === 'Windows' && t.windowsRequirements) requirements.push(...t.windowsRequirements);
      else if (pinfo.defaultTaskRequirements) requirements.push(...pinfo.defaultTaskRequirements);
      if (pinfo.lafRequirement && t.windowsLaf) requirements.push(pinfo.lafRequirement);
    } else if (m.providerRemarks) {
      requirements.push(m.providerRemarks);
    }
    if (t.pragma) requirements.push(`The ${t.text.toLowerCase()} API is experimental: suppress ${t.pragma} to use it.`);

    const codeTabs = [
      { label: 'Quick start', content: codeBlock(quickStart(m), 'C#') },
      { label: 'Check availability', content: codeBlock(availabilitySnippet(m), 'C#') },
    ];
    if (fallbackPartners.length) {
      const ordered = fallbackChain([m, ...fallbackPartners]);
      codeTabs.push({ label: 'With fallback', content: codeBlock(fallbackSnippet(ordered[0], ordered.slice(1)), 'C#') });
    }
    if (t.liveUsage) codeTabs.push({ label: 'Live audio', content: codeBlock(liveSnippet(m), 'C#') });
    if (t.di) codeTabs.push({ label: 'Dependency injection', content: codeBlock(diSnippet(m), 'C#') });

    const fallbackPackages = [...new Set(fallbackPartners.map(x => x.package))].filter(p => p !== m.package);

    app.replaceChildren(
      h('nav', { class: 'crumbs', 'aria-label': 'Breadcrumb' },
        h('a', { href: '#/' }, 'Tasks'), ' / ', h('a', { href: taskHref(t) }, t.text), ' / ', m.displayName),
      h('section', { class: 'hero model-hero' },
        h('div', { class: 'chips' },
          h('a', { class: 'task-tag', href: taskHref(t) }, `${t.icon} ${t.text}`),
          m.crossPlatform ? badge('✓ Cross-platform validated', 'xplat', 'Validated cross-platform by the conformance suite') : null,
          t.experimental ? badge('Experimental API', 'experimental', `Suppress ${t.pragma} to use it`) : null,
          m.retired ? badge('Retired', 'retired') : null),
        h('h1', null, m.displayName),
        m.publisher ? h('p', { class: 'meta' }, `by ${m.publisher}`) : null,
        h('p', { class: 'lead' }, m.description),
        m.retired ? h('div', { class: 'notice warn' }, h('strong', null, 'Retired. '), m.retired.message || '',
          m.retired.replacement ? [' Use ', h('code', null, `${t.catalogClass}.${m.retired.replacement}`), ' instead.'] : null) : null),
      h('div', { class: 'detail-grid' },
        h('div', null,
          h('h2', null, 'Overview'),
          h('table', { class: 'kv' }, h('tbody', null,
            h('tr', null, h('th', { scope: 'row' }, 'Catalog handle'), h('td', null, h('code', null, m.handle), copyButton(() => m.handle))),
            h('tr', null, h('th', { scope: 'row' }, 'Alias'), h('td', null, h('code', null, m.alias))),
            h('tr', null, h('th', { scope: 'row' }, 'Package'), h('td', null, h('code', null, m.package))),
            h('tr', null, h('th', { scope: 'row' }, 'Task package'), h('td', null, h('code', null, m.taskPackage))),
            h('tr', null, h('th', { scope: 'row' }, 'Model interface'), h('td', null, h('code', null, t.modelInterface))),
            h('tr', null, h('th', { scope: 'row' }, 'Client'), h('td', null, h('code', null, t.clientType))),
            m.publisher ? h('tr', null, h('th', { scope: 'row' }, 'Publisher'), h('td', null, m.publisher)) : null)),
          pinfo ? h('p', { class: 'small muted' }, pinfo.blurb) : null,

          h('h2', null, 'Capabilities'),
          h('ul', { class: 'cap-list' },
            CAPABILITIES.map(c => h('li', { class: m.capabilities[c.key] ? 'yes' : 'no' },
              h('span', { class: 'mark', 'aria-hidden': 'true' }, m.capabilities[c.key] ? '✓' : '—'),
              h('span', null, c.label), h('span', { class: 'sr-only' }, m.capabilities[c.key] ? ': supported' : ': not supported'))),
            m.contextLength ? h('li', { class: 'yes' }, h('span', { class: 'mark', 'aria-hidden': 'true' }, '↔'),
              h('span', null, `Context length: ${m.contextLength.toLocaleString('en-US')} tokens (${formatTokens(m.contextLength)})`)) : null,
            m.maxOutputTokens ? h('li', { class: 'yes' }, h('span', { class: 'mark', 'aria-hidden': 'true' }, '↦'),
              h('span', null, `Max output: ${m.maxOutputTokens.toLocaleString('en-US')} tokens`)) : null),

          h('h2', null, 'Supported platforms'),
          h('table', { class: 'platform-matrix' },
            h('thead', null, h('tr', null, h('th', { scope: 'col' }, 'OS'), h('th', { scope: 'col' }, 'Architecture'), h('th', { scope: 'col' }, 'RID'), h('th', { scope: 'col' }, 'Supported'))),
            h('tbody', null, PLATFORMS.map(p => {
              const ok = m.platforms.includes(p.rid);
              return h('tr', { class: ok ? 'yes' : 'no' },
                h('td', null, p.os), h('td', null, p.arch), h('td', null, h('code', null, p.rid)),
                h('td', null, ok ? '✓ Yes' : '— No'));
            }))),
          h('p', { class: 'small muted' }, m.crossPlatform
            ? 'Validated cross-platform by the conformance suite: the same code and package run on Windows and macOS.'
            : (m.provider === 'Windows'
              ? 'Off Windows, or in a net8.0 (non-Windows TFM) build, the handle reports NotSupportedOnPlatform, so cross-platform code compiles without #if.'
              : 'This model is not yet validated cross-platform by the conformance suite.')),

          h('h2', null, 'Requirements'),
          h('ul', { class: 'req-list' }, requirements.map(r => h('li', null, r))),
          t.providerNotes && t.providerNotes[m.provider] ? [h('h2', null, 'Audio input'), h('p', null, t.providerNotes[m.provider])] : null),

        h('div', null,
          h('h2', null, 'Install'),
          tabs([
            { label: '.NET CLI', content: codeBlock(installCli([m.package]), 'shell') },
            { label: 'PackageReference', content: codeBlock(packageReferences([m.package]), 'XML') },
            { label: 'Project file', content: codeBlock(projectFile(m), 'XML') },
          ]),
          h('p', { class: 'small muted' }, `${m.package} brings in ${m.taskPackage} and the core Microsoft.AI.Local package. `,
            'Until the packages are on NuGet.org, build them with ', h('code', null, 'pack.ps1'), ' and add ',
            h('code', null, '--source <repo>/artifacts/packages'), '.'),
          t.livePackage ? h('p', { class: 'small muted' }, 'For live audio (microphone capture or a PushAudioStream), also add ',
            h('code', null, t.livePackage), '.') : null,
          h('h2', null, 'Code'),
          tabs(codeTabs),
          fallbackPackages.length ? h('p', { class: 'small muted' }, 'The fallback example also needs ',
            fallbackPackages.map((p, i) => [i ? ', ' : '', h('code', null, p)]), '.') : null)),

      others.length ? h('section', null,
        h('h2', null, `Other ${t.text.toLowerCase()} models`),
        h('div', { class: 'grid' }, others.slice(0, 6).map(x => modelCard(x, false))),
        others.length > 6 ? h('p', null, h('a', { href: taskHref(t) }, `See all ${others.length + 1} ${t.text.toLowerCase()} models →`)) : null) : null);
  }

  function renderStart() {
    setNav('start');
    setTitle('Get started');
    const phi = catalog.models.find(m => m.handle === 'LanguageModels.Phi4Mini') || catalog.models.find(m => m.task.name === 'text-generation');
    const silica = catalog.models.find(m => m.handle === 'LanguageModels.PhiSilica');
    const packages = [...new Set([silica, phi].filter(Boolean).map(m => m.package))];
    app.replaceChildren(
      h('section', { class: 'hero' }, h('h1', null, 'Get started'),
        h('p', { class: 'lead' }, 'Microsoft.AI.Local gives every local model a strongly-typed handle in a per-task catalog class. Code against the task once; the handle is the only model-specific line.')),
      h('ol', { class: 'steps' },
        h('li', null, h('h2', null, 'Pick a task and a model'),
          h('p', null, h('a', { href: '#/' }, 'Browse the tasks'), ' and pick the platforms your app runs on to see only what works there. ',
            'Each task lists its models with filters for platform, capabilities and more; ', h('strong', null, 'Cross-platform validated'),
            ' models are tested on Windows and macOS by the conformance suite.')),
        h('li', null, h('h2', null, 'Add the model\'s package'),
          h('p', null, 'Each model page names the package to reference, and that package brings in everything else the task needs. For example, for ',
            phi ? h('code', null, phi.handle) : 'a chat model', silica ? [' and ', h('code', null, silica.handle)] : null, ':'),
          codeBlock(installCli(packages.length ? packages : ['Microsoft.AI.Local.TextGeneration.Foundry']), 'shell'),
          h('p', { class: 'small muted' }, 'Building from source? Run ', h('code', null, '.\\pack.ps1'), ' at the repository root and add ',
            h('code', null, '--source <repo>\\artifacts\\packages'), ' to the commands above.')),
        h('li', null, h('h2', null, 'Write the code once'),
          h('p', null, 'Use the model built into Windows on Copilot+ PCs, and fall back to a cross-platform model everywhere else:'),
          phi && silica ? codeBlock(fallbackSnippet(silica, [phi]), 'C#') : (phi ? codeBlock(quickStart(phi), 'C#') : null),
          h('p', null, 'Using a handle whose package isn\'t referenced is a build warning (', h('code', null, 'MSAILOCAL201'),
            ') and reports ', h('code', null, 'MissingAppRequirement'), ' at run time, never an exception.')),
        h('li', null, h('h2', null, 'Learn more'),
          h('ul', null,
            h('li', null, h('a', { href: `${REPO_URL}#readme` }, 'Repository README')),
            h('li', null, h('a', { href: `${REPO_URL}/blob/main/PROPOSAL.md` }, 'Design proposal')),
            h('li', null, h('a', { href: `${REPO_URL}/blob/main/ARCHITECTURE.md` }, 'Architecture')),
            h('li', null, h('a', { href: `${REPO_URL}/tree/main/eng/catalog` }, 'Model manifests (eng/catalog)'))))));
  }

  function renderNotFound() {
    setTitle('Not found');
    app.replaceChildren(h('section', { class: 'hero' }, h('h1', null, 'Not found'),
      h('p', null, 'That page doesn\'t exist. ', h('a', { href: '#/' }, 'Browse all tasks'), '.')));
  }

  // ---------- router ----------

  let lastRoute = '';
  function route() {
    const hash = location.hash.replace(/^#/, '') || '/';
    const [path, query = ''] = hash.split('?');
    const parts = path.split('/').filter(Boolean).map(decodeURIComponent);
    // Links from the earlier, model-first version of the site.
    if (parts.length === 0 && new URLSearchParams(query).get('task')) {
      const params = new URLSearchParams(query);
      const task = params.get('task').split(',')[0];
      params.delete('task');
      params.delete('provider');
      const rest = params.toString();
      location.replace(`#/task/${encodeURIComponent(task)}${rest ? `?${rest}` : ''}`);
      return;
    }
    if (parts[0] === 'tasks' && parts.length === 1) { location.replace('#/'); return; }
    if (parts[0] === 'model' && parts.length === 3) {
      const m = catalog.models.find(x => x.providerPrefix === parts[1] && x.alias === parts[2]);
      if (m) { location.replace(modelHref(m)); return; }
    }
    const routeKey = parts.join('/');
    try {
      if (parts.length === 0) renderHome(query);
      else if (parts[0] === 'task' && parts.length === 2) renderTask(parts[1], query);
      else if (parts[0] === 'task' && parts.length === 3) {
        renderModel(catalog.models.find(x => x.task.name === parts[1] && x.property === parts[2]));
      } else if (parts[0] === 'start' && parts.length === 1) renderStart();
      else renderNotFound();
    } catch (e) {
      console.error(e);
      app.replaceChildren(h('p', { class: 'notice warn' }, `Something went wrong: ${e.message}`));
    }
    if (routeKey !== lastRoute) {
      window.scrollTo(0, 0);
      lastRoute = routeKey;
    }
  }

  window.addEventListener('hashchange', route);

  loadCatalog().then(data => {
    catalog = data;
    route();
  }).catch(e => {
    console.error(e);
    app.replaceChildren(h('div', { class: 'notice warn' },
      h('p', null, h('strong', null, 'Couldn\'t load the model catalog. '), e.message),
      h('p', null, 'To preview locally, serve the repository root over HTTP (for example ', h('code', null, 'python -m http.server'),
        ') and open ', h('code', null, '/site/'), '.')));
  });
})();
