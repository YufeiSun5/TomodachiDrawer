import React, { useEffect, useMemo, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  CheckCircle2,
  Download,
  FileImage,
  Github,
  Heart,
  Home,
  ImageUp,
  Languages,
  Loader2,
  Play,
  Search,
  ShieldCheck,
  SlidersHorizontal,
  Sparkles,
  Trash2
} from "lucide-react";
import "./styles.css";

type JobStatus = "pending" | "running" | "success" | "failed" | "cancelled" | "expired";

type Job = {
  jobUuid: string;
  fileName: string;
  boardType: string;
  outputType: string;
  colourMatcher: string;
  tspTimeLimit: number;
  status: JobStatus;
  queuePosition: number;
  queueAhead: number;
  progressPercent: number;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  downloadUrl?: string;
  previewUrl?: string;
  message?: string;
};

type ImageMetrics = {
  width: number;
  height: number;
};

type CropControls = {
  centerX: number;
  centerY: number;
  zoom: number;
};

type CropRect = {
  x: number;
  y: number;
  size: number;
};

type GallerySort = "popular" | "latest" | "random";

const viteBase = import.meta.env.BASE_URL.replace(/\/$/, "");
const apiBase = import.meta.env.VITE_API_BASE ?? (viteBase === "" || viteBase === "/" ? "" : viteBase);

type GalleryItem = {
  galleryId: string;
  title: string;
  boardType: string;
  outputType: string;
  createdAt: string;
  likes: number;
  previewUrl: string;
  downloadUrl: string;
};

const boardFilters = [
  { value: "all", label: "全部型号" },
  { value: "rp2040", label: "树莓派 RP2040" },
  { value: "rp2350", label: "树莓派 RP2350" },
  { value: "esp32-s3", label: "ESP32-S3" }
];

function App() {
  const [clientId] = useState(() => getOrCreateClientId());
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const imageRef = useRef<HTMLImageElement | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [imageVersion, setImageVersion] = useState(0);
  const [imageMetrics, setImageMetrics] = useState<ImageMetrics | null>(null);
  const [crop, setCrop] = useState<CropControls>({ centerX: 0.5, centerY: 0.5, zoom: 1 });
  const [boardType, setBoardType] = useState("rp2040");
  const [outputType, setOutputType] = useState("tdld");
  const [switchVersion, setSwitchVersion] = useState("switch2");
  const [colourMatcher, setColourMatcher] = useState("arbitrary");
  const [tspTimeLimit, setTspTimeLimit] = useState(30);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [galleryQuery, setGalleryQuery] = useState("");
  const [galleryBoard, setGalleryBoard] = useState("all");
  const [gallerySort, setGallerySort] = useState<GallerySort>("popular");
  const [galleryItems, setGalleryItems] = useState<GalleryItem[]>([]);
  const [shareTitles, setShareTitles] = useState<Record<string, string>>({});
  const [shareBusyJobUuid, setShareBusyJobUuid] = useState<string | null>(null);
  const [shareMessage, setShareMessage] = useState<string | null>(null);

  const selectedFileMeta = useMemo(() => {
    if (!file) return null;
    return {
      name: file.name,
      size: `${Math.max(1, Math.round(file.size / 1024))} KB`,
      type: file.type || "unknown"
    };
  }, [file]);

  const cropRect = useMemo(() => computeCropRect(imageMetrics, crop), [imageMetrics, crop]);
  const activeJobs = useMemo(
    () => jobs.filter((job) => job.status === "pending" || job.status === "running"),
    [jobs]
  );
  const completedJobs = useMemo(
    () => jobs.filter((job) => job.status === "success" || job.status === "failed"),
    [jobs]
  );

  const filteredGallery = useMemo(() => {
    if (gallerySort === "latest") {
      return [...galleryItems].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    }

    if (gallerySort === "random") {
      return [...galleryItems].sort((a, b) => stableRandomKey(a.galleryId) - stableRandomKey(b.galleryId));
    }

    return [...galleryItems].sort((a, b) => b.likes - a.likes);
  }, [galleryItems, gallerySort]);

  useEffect(() => {
    if (boardType === "esp32-s3" && outputType === "uf2") {
      setOutputType("tdld");
    }
  }, [boardType, outputType]);

  useEffect(() => {
    let cancelled = false;

    async function loadRecentJobs() {
      try {
        const response = await fetch(`${apiBase}/api/jobs?clientId=${encodeURIComponent(clientId)}`);
        if (!response.ok) return;
        const recentJobs = (await response.json()) as Job[];
        if (!cancelled) {
          setJobs(recentJobs);
        }
      } catch {
        // The page still works for newly submitted jobs if loading old in-memory jobs fails.
      }
    }

    loadRecentJobs();
    return () => {
      cancelled = true;
    };
  }, [clientId]);

  useEffect(() => {
    let cancelled = false;

    async function loadGallery() {
      const params = new URLSearchParams();
      params.set("boardType", galleryBoard);
      params.set("sort", gallerySort === "random" ? "latest" : gallerySort);
      if (galleryQuery.trim()) {
        params.set("q", galleryQuery.trim());
      }

      try {
        const response = await fetch(`${apiBase}/api/gallery?${params.toString()}`);
        if (!response.ok) return;
        const items = (await response.json()) as GalleryItem[];
        if (!cancelled) {
          setGalleryItems(items);
        }
      } catch {
        if (!cancelled) {
          setGalleryItems([]);
        }
      }
    }

    loadGallery();
    return () => {
      cancelled = true;
    };
  }, [galleryBoard, galleryQuery, gallerySort]);

  useEffect(() => {
    if (activeJobs.length === 0) return;

    const timer = window.setInterval(async () => {
      const updates = await Promise.all(
        activeJobs.map(async (job) => {
          try {
            const response = await fetch(`${apiBase}/api/jobs/${job.jobUuid}?clientId=${encodeURIComponent(clientId)}`);
            return response.ok ? ((await response.json()) as Job) : job;
          } catch {
            return job;
          }
        })
      );

      setJobs((current) =>
        current.map((job) => updates.find((update) => update.jobUuid === job.jobUuid) ?? job)
      );
    }, 2500);

    return () => window.clearInterval(timer);
  }, [activeJobs, clientId]);

  useEffect(() => {
    if (!previewUrl) {
      imageRef.current = null;
      setImageMetrics(null);
      return;
    }

    const image = new Image();
    image.onload = () => {
      imageRef.current = image;
      setImageMetrics({ width: image.naturalWidth, height: image.naturalHeight });
      setCrop({ centerX: 0.5, centerY: 0.5, zoom: 1 });
      setImageVersion((current) => current + 1);
    };
    image.src = previewUrl;
  }, [previewUrl]);

  useEffect(() => {
    const canvas = canvasRef.current;
    const image = imageRef.current;
    if (!canvas || !image || !cropRect) return;

    const context = canvas.getContext("2d");
    if (!context) return;

    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.imageSmoothingEnabled = true;
    context.drawImage(image, cropRect.x, cropRect.y, cropRect.size, cropRect.size, 0, 0, canvas.width, canvas.height);
  }, [cropRect, imageVersion]);

  useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  }, [previewUrl]);

  function chooseFile(nextFile: File | null) {
    setError(null);
    setFile(nextFile);
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setPreviewUrl(nextFile ? URL.createObjectURL(nextFile) : null);
  }

  async function createJob() {
    if (!file || !cropRect) {
      setError("请先上传图片，并在裁切框里确认要绘制的区域。");
      return;
    }

    setBusy(true);
    setError(null);

    try {
      const form = new FormData();
      form.append("image", file);
      form.append("boardType", boardType);
      form.append("outputType", outputType);
      form.append("switchVersion", switchVersion);
      form.append("colourMatcher", colourMatcher);
      form.append("tspTimeLimit", String(tspTimeLimit));
      form.append("clientId", clientId);
      form.append("cropX", cropRect.x.toFixed(3));
      form.append("cropY", cropRect.y.toFixed(3));
      form.append("cropSize", cropRect.size.toFixed(3));

      const response = await fetch(`${apiBase}/api/jobs`, {
        method: "POST",
        body: form
      });

      if (!response.ok) {
        throw new Error(await response.text());
      }

      const job = (await response.json()) as Job;
      setJobs((current) => [job, ...current]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "任务创建失败。");
    } finally {
      setBusy(false);
    }
  }

  async function shareToGallery(job: Job) {
    const title = (shareTitles[job.jobUuid] || "").trim() || "未命名作品";
    setShareBusyJobUuid(job.jobUuid);
    setShareMessage(null);

    try {
      const form = new FormData();
      form.append("jobUuid", job.jobUuid);
      form.append("clientId", clientId);
      form.append("title", title);

      const response = await fetch(`${apiBase}/api/gallery`, {
        method: "POST",
        body: form
      });

      if (!response.ok) {
        throw new Error(await response.text());
      }

      const item = (await response.json()) as GalleryItem;
      setGalleryItems((current) => [item, ...current]);
      setShareMessage("已分享到广场。");
    } catch (err) {
      setShareMessage(err instanceof Error ? err.message : "分享失败。");
    } finally {
      setShareBusyJobUuid(null);
    }
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <a className="brand" href="#studio">
          <span className="brand-mark">
            <Home size={22} />
          </span>
          <span>
            <strong>TomodachiDrawer-CN</strong>
            <small>朋友收集梦想生活 · 画作转换</small>
          </span>
        </a>
        <nav>
          <a href="#gallery">广场</a>
          <a href="#studio">创作</a>
          <a href="https://github.com/Lucas7yoshi/TomodachiDrawer" target="_blank" rel="noreferrer">
            <Github size={17} /> GPL-3.0
          </a>
          <span className="language-pill">
            <Languages size={16} /> 简体中文
          </span>
        </nav>
      </header>

      <section className="hero">
        <div>
          <p className="eyebrow"><Sparkles size={16} /> 公开测试版</p>
          <h1>把任意尺寸图片裁进 1:1 画布，再生成单片机绘画文件</h1>
          <p>
            图片比例不对时，先在方形框里选取要画的部分。生成后可以临时下载，也可以命名分享到广场。
          </p>
        </div>
        <div className="status-strip" aria-label="当前能力">
          <span><CheckCircle2 size={16} /> 大图裁切</span>
          <span><CheckCircle2 size={16} /> TDLD / UF2</span>
          <span><ShieldCheck size={16} /> 公开前审核</span>
        </div>
      </section>

      <section className="gallery-section" id="gallery">
        <div className="section-heading">
          <span>
            <Heart size={22} />
            <strong>分享广场</strong>
          </span>
          <p>公开作品会显示缩略图、适用单片机型号和下载格式；作品名称允许重复。</p>
        </div>

        <div className="gallery-toolbar">
          <label className="search-box">
            <Search size={17} />
            <input
              value={galleryQuery}
              placeholder="搜索作品名称"
              onChange={(event) => setGalleryQuery(event.target.value)}
            />
          </label>
          <Segmented
            value={gallerySort}
            onChange={(value) => setGallerySort(value as GallerySort)}
            options={["popular", "latest", "random"]}
          />
        </div>

        <div className="category-row">
          {boardFilters.map((board) => (
            <button
              type="button"
              className={board.value === galleryBoard ? "selected" : ""}
              onClick={() => setGalleryBoard(board.value)}
              key={board.value}
            >
              {board.label}
            </button>
          ))}
        </div>

        <div className="gallery-grid">
          {filteredGallery.length === 0 && (
            <div className="empty-gallery">
              <strong>广场还没有作品</strong>
              <span>生成完成后可以把自己的作品命名并分享到这里。</span>
            </div>
          )}
          {filteredGallery.map((item) => (
            <article className="gallery-card" key={item.galleryId}>
              <img className="gallery-thumb" src={`${apiBase}${item.previewUrl}`} alt={item.title} />
              <div>
                <strong>{item.title}</strong>
                <small>{boardLabel(item.boardType)} · {item.outputType.toUpperCase()}</small>
              </div>
              <span className="like-pill"><Heart size={15} /> {item.likes}</span>
              <a className="ghost-button gallery-download" href={`${apiBase}${item.downloadUrl}`}>
                <Download size={15} /> 下载
              </a>
            </article>
          ))}
        </div>
      </section>

      <section className="studio-grid" id="studio">
        <section className="tool-panel upload-panel">
          <PanelTitle icon={<ImageUp size={18} />} title="图片与裁切" />
          <label
            className="dropzone"
            onDragOver={(event) => event.preventDefault()}
            onDrop={(event) => {
              event.preventDefault();
              chooseFile(event.dataTransfer.files.item(0));
            }}
          >
            <input
              type="file"
              accept="image/png,image/jpeg,image/webp"
              onChange={(event) => chooseFile(event.target.files?.item(0) ?? null)}
            />
            <canvas ref={canvasRef} className="crop-canvas" width="256" height="256" />
            {!previewUrl && (
              <div className="dropzone-empty">
                <ImageUp size={40} />
                <strong>点击或拖拽图片</strong>
                <span>PNG / JPG / WEBP，单文件 8MB 内</span>
              </div>
            )}
          </label>

          {selectedFileMeta && (
            <div className="file-card">
              <FileImage size={18} />
              <span>
                <strong>{selectedFileMeta.name}</strong>
                <small>{selectedFileMeta.type} · {selectedFileMeta.size}</small>
              </span>
              <button className="icon-button danger" onClick={() => chooseFile(null)} aria-label="移除图片">
                <Trash2 size={16} />
              </button>
            </div>
          )}

          <div className="crop-controls">
            <Field label={`缩放：${crop.zoom.toFixed(1)}x`}>
              <input
                type="range"
                min="1"
                max="4"
                step="0.1"
                value={crop.zoom}
                disabled={!previewUrl}
                onChange={(event) => setCrop((current) => ({ ...current, zoom: Number(event.target.value) }))}
              />
            </Field>
            <Field label="左右取景">
              <input
                type="range"
                min="0"
                max="1"
                step="0.01"
                value={crop.centerX}
                disabled={!previewUrl}
                onChange={(event) => setCrop((current) => ({ ...current, centerX: Number(event.target.value) }))}
              />
            </Field>
            <Field label="上下取景">
              <input
                type="range"
                min="0"
                max="1"
                step="0.01"
                value={crop.centerY}
                disabled={!previewUrl}
                onChange={(event) => setCrop((current) => ({ ...current, centerY: Number(event.target.value) }))}
              />
            </Field>
          </div>
        </section>

        <section className="tool-panel settings-panel">
          <PanelTitle icon={<SlidersHorizontal size={18} />} title="生成设置" />
          <Field label="单片机类型">
            <Segmented value={boardType} onChange={setBoardType} options={["rp2040", "rp2350", "esp32-s3"]} />
          </Field>
          <Field label="Switch 版本">
            <Segmented value={switchVersion} onChange={setSwitchVersion} options={["switch2", "switch1"]} />
          </Field>
          <Field label="输出文件">
            <Segmented
              value={outputType}
              onChange={setOutputType}
              options={boardType === "esp32-s3" ? ["tdld"] : ["tdld", "uf2"]}
            />
          </Field>
          <Field label="颜色匹配">
            <select value={colourMatcher} onChange={(event) => setColourMatcher(event.target.value)}>
              <option value="arbitrary">自动匹配（推荐）</option>
              <option value="cielab">CIE Lab</option>
              <option value="redmean">Redmean</option>
              <option value="euclidean">Euclidean</option>
            </select>
          </Field>
          <Field label={`路线规划：${tspTimeLimit}s`}>
            <input
              type="range"
              min="10"
              max="120"
              step="10"
              value={tspTimeLimit}
              onChange={(event) => setTspTimeLimit(Number(event.target.value))}
            />
          </Field>

          {error && <div className="error-box">{error}</div>}

          <button className="primary-action" onClick={createJob} disabled={busy}>
            {busy ? <Loader2 className="spin" size={18} /> : <Play size={18} />}
            开始生成
          </button>
        </section>

        <section className="tool-panel jobs-panel">
          <PanelTitle icon={<Loader2 size={18} />} title="实时队列" />
          <div className="job-list">
            {activeJobs.length === 0 && <EmptyJobs />}
            {activeJobs.map((job, index) => (
              <article className={`job-card ${job.status}`} key={job.jobUuid}>
                <span className="room-number">{String(index + 1).padStart(2, "0")}</span>
                {job.previewUrl && (
                  <img className="job-thumb" src={`${apiBase}${job.previewUrl}`} alt="我的任务缩略图" />
                )}
                <span className="job-copy">
                  <strong>{jobStatusText(job)}</strong>
                  <small>{job.fileName}</small>
                  {job.boardType && <small>{boardLabel(job.boardType)} · {job.outputType.toUpperCase()} · {job.colourMatcher}</small>}
                  <small>{jobQueueText(job)}</small>
                  <span className="progress-track" aria-label={`任务进度 ${job.progressPercent}%`}>
                    <span style={{ width: `${Math.max(2, job.progressPercent)}%` }} />
                  </span>
                  {job.message && <small className="job-message">{formatJobMessage(job.message)}</small>}
                </span>
                <span className="job-actions">
                  {job.previewUrl && (
                    <a className="ghost-button" href={`${apiBase}${job.previewUrl}`} target="_blank" rel="noreferrer">
                      预览
                    </a>
                  )}
                  {job.downloadUrl && (
                    <a className="download-button" href={`${apiBase}${job.downloadUrl}`}>
                      <Download size={16} /> 下载
                    </a>
                  )}
                </span>
              </article>
            ))}
          </div>
          {completedJobs.length > 0 && (
            <div className="temporary-downloads">
              <strong>临时下载</strong>
              <small>未投稿结果不会进入历史列表，下载窗口约 30 分钟。</small>
              {completedJobs.slice(0, 5).map((job) => (
                <article className={`job-card compact ${job.status}`} key={job.jobUuid}>
                  {job.previewUrl && (
                    <img className="job-thumb" src={`${apiBase}${job.previewUrl}`} alt="我的生成缩略图" />
                  )}
                  <span className="job-copy">
                    <strong>{jobStatusText(job)}</strong>
                    <small>{job.fileName}</small>
                    {job.message && <small className="job-message">{formatJobMessage(job.message)}</small>}
                    {job.status === "success" && (
                      <span className="share-row">
                        <input
                          value={shareTitles[job.jobUuid] ?? ""}
                          placeholder="广场名称，可重复"
                          onChange={(event) =>
                            setShareTitles((current) => ({ ...current, [job.jobUuid]: event.target.value }))
                          }
                        />
                        <button
                          type="button"
                          className="ghost-button"
                          disabled={shareBusyJobUuid === job.jobUuid}
                          onClick={() => shareToGallery(job)}
                        >
                          {shareBusyJobUuid === job.jobUuid ? "分享中" : "分享到广场"}
                        </button>
                      </span>
                    )}
                  </span>
                  <span className="job-actions">
                    {job.previewUrl && (
                      <a className="ghost-button" href={`${apiBase}${job.previewUrl}`} target="_blank" rel="noreferrer">
                        预览
                      </a>
                    )}
                    {job.downloadUrl && (
                      <a className="download-button" href={`${apiBase}${job.downloadUrl}`}>
                        <Download size={16} /> 下载
                      </a>
                    )}
                  </span>
                </article>
              ))}
            </div>
          )}
          {shareMessage && <div className="note-box">{shareMessage}</div>}
        </section>
      </section>

      <footer className="site-footer">
        <span><ShieldCheck size={18} /> 基于 TomodachiDrawer 构建，遵循 GPL-3.0。</span>
        <span>本站与 Nintendo 无官方关联。</span>
      </footer>
    </main>
  );
}

function computeCropRect(metrics: ImageMetrics | null, crop: CropControls): CropRect | null {
  if (!metrics) return null;
  const baseSize = Math.min(metrics.width, metrics.height);
  const size = clamp(baseSize / crop.zoom, 1, baseSize);
  const x = clamp(metrics.width * crop.centerX - size / 2, 0, metrics.width - size);
  const y = clamp(metrics.height * crop.centerY - size / 2, 0, metrics.height - size);
  return { x, y, size };
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

function stableRandomKey(value: string) {
  return Array.from(value).reduce((total, char) => total + char.charCodeAt(0), 0) % 97;
}

function getOrCreateClientId() {
  const key = "tomodachi-cn-client-id";
  const existing = window.localStorage.getItem(key);
  if (existing) return existing;

  const generated = createClientId();
  window.localStorage.setItem(key, generated);
  return generated;
}

function createClientId() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }

  if (globalThis.crypto?.getRandomValues) {
    const bytes = new Uint8Array(16);
    globalThis.crypto.getRandomValues(bytes);
    return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
  }

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function boardLabel(boardType: string) {
  if (boardType === "rp2350") return "树莓派 RP2350";
  if (boardType === "esp32-s3") return "ESP32-S3";
  return "树莓派 RP2040";
}

function jobStatusText(job: Job) {
  if (job.status === "success") return "文件已生成";
  if (job.status === "running") return "正在生成";
  if (job.status === "failed") return "生成失败";
  if (job.status === "pending") return "正在排队";
  return job.status;
}

function jobQueueText(job: Job) {
  if (job.status === "pending") {
    return `前面还有 ${job.queueAhead} 个任务，你是第 ${job.queuePosition} 位`;
  }

  if (job.status === "running") {
    return "当前正在生成";
  }

  if (job.status === "success") {
    return "可以预览和下载";
  }

  return "请检查提示后重新提交";
}

function formatJobMessage(message: string) {
  const layerMatch = message.match(/\[(\d+)\/(\d+)\]\s+\(([^)]+)\).*->\s+(\w+)/);
  if (layerMatch) {
    const [, current, total, colour, route] = layerMatch;
    const routeName = route.toLowerCase() === "tsp" ? "优化路线" : "顺序路线";
    return `正在处理第 ${current}/${total} 个颜色层，颜色 RGB(${colour})，选择${routeName}。`;
  }

  if (message.includes("Stamps:")) {
    return "正在计算大色块和印章路线。";
  }

  return message;
}

function PanelTitle({ icon, title }: { icon: React.ReactNode; title: string }) {
  return <h2 className="panel-title">{icon}{title}</h2>;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
    </label>
  );
}

function Segmented({ value, options, onChange }: { value: string; options: string[]; onChange: (value: string) => void }) {
  const labels: Record<string, string> = {
    popular: "点赞最多",
    latest: "最新发布",
    random: "随机推荐"
  };

  return (
    <div className="segmented">
      {options.map((option) => (
        <button
          type="button"
          className={option === value ? "selected" : ""}
          onClick={() => onChange(option)}
          key={option}
        >
          {labels[option] ?? option.toUpperCase()}
        </button>
      ))}
    </div>
  );
}

function EmptyJobs() {
  return (
    <div className="empty-jobs">
      <strong>还没有生成记录</strong>
      <span>上传图片、确认裁切后，文件会出现在这里。</span>
    </div>
  );
}

createRoot(document.getElementById("root")!).render(<App />);
