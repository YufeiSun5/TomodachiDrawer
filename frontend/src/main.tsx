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
  Palette,
  Play,
  Search,
  ShieldCheck,
  Shuffle,
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

const apiBase = import.meta.env.VITE_API_BASE ?? "";

const galleryItems = [
  { title: "海边小屋", category: "建筑", likes: 328, date: "2026-06-02", tone: "aqua" },
  { title: "午后甜点", category: "食物", likes: 214, date: "2026-06-05", tone: "peach" },
  { title: "星星衬衫", category: "衣服", likes: 188, date: "2026-05-28", tone: "lemon" },
  { title: "圆圆头像", category: "脸绘", likes: 171, date: "2026-06-01", tone: "pink" },
  { title: "电视节目 Logo", category: "电视节目", likes: 89, date: "2026-06-04", tone: "blue" },
  { title: "宠物相册", category: "宠物", likes: 73, date: "2026-05-25", tone: "green" }
];

const categories = ["全部", "食物", "宠物", "专辑", "脸绘", "衣服", "建筑", "墙纸", "道路", "游戏", "电视节目", "书籍", "其他"];

function App() {
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
  const [galleryCategory, setGalleryCategory] = useState("全部");
  const [gallerySort, setGallerySort] = useState<GallerySort>("popular");

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
    const query = galleryQuery.trim().toLowerCase();
    const matched = galleryItems.filter((item) => {
      const categoryMatched = galleryCategory === "全部" || item.category === galleryCategory;
      const queryMatched = !query || `${item.title} ${item.category}`.toLowerCase().includes(query);
      return categoryMatched && queryMatched;
    });

    if (gallerySort === "latest") {
      return [...matched].sort((a, b) => b.date.localeCompare(a.date));
    }

    if (gallerySort === "random") {
      return [...matched].sort((a, b) => stableRandomKey(a.title) - stableRandomKey(b.title));
    }

    return [...matched].sort((a, b) => b.likes - a.likes);
  }, [galleryCategory, galleryQuery, gallerySort]);

  useEffect(() => {
    if (boardType === "esp32-s3" && outputType === "uf2") {
      setOutputType("tdld");
    }
  }, [boardType, outputType]);

  useEffect(() => {
    let cancelled = false;

    async function loadRecentJobs() {
      try {
        const response = await fetch(`${apiBase}/api/jobs`);
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
  }, []);

  useEffect(() => {
    if (activeJobs.length === 0) return;

    const timer = window.setInterval(async () => {
      const updates = await Promise.all(
        activeJobs.map(async (job) => {
          try {
            const response = await fetch(`${apiBase}/api/jobs/${job.jobUuid}`);
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
  }, [activeJobs]);

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
          <a href="#studio">生成</a>
          <a href="#gallery">广场</a>
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
            图片比例不对时，先在方形框里选取要画的部分。通过审核的作品后续会同时保留预览图和可下载文件。
          </p>
        </div>
        <div className="status-strip" aria-label="当前能力">
          <span><CheckCircle2 size={16} /> 大图裁切</span>
          <span><CheckCircle2 size={16} /> TDLD / UF2</span>
          <span><ShieldCheck size={16} /> 公开前审核</span>
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
                <span className="job-copy">
                  <strong>{jobStatusText(job)}</strong>
                  <small>{job.fileName}</small>
                  <small>{job.boardType} · {job.outputType.toUpperCase()} · {job.colourMatcher}</small>
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
                  <span className="job-copy">
                    <strong>{jobStatusText(job)}</strong>
                    <small>{job.fileName}</small>
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
          )}
        </section>
      </section>

      <section className="gallery-section" id="gallery">
        <div className="section-heading">
          <span>
            <Heart size={22} />
            <strong>分享广场</strong>
          </span>
          <p>作品公开前必须审核。当前广场是前端示例数据，用于验证搜索、分类和排序体验。</p>
        </div>

        <div className="gallery-toolbar">
          <label className="search-box">
            <Search size={17} />
            <input
              value={galleryQuery}
              placeholder="搜索作品或分类"
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
          {categories.map((category) => (
            <button
              type="button"
              className={category === galleryCategory ? "selected" : ""}
              onClick={() => setGalleryCategory(category)}
              key={category}
            >
              {category}
            </button>
          ))}
        </div>

        <div className="gallery-grid">
          {filteredGallery.map((item) => (
            <article className="gallery-card" key={item.title}>
              <div className={`sample-art ${item.tone}`}>
                {gallerySort === "random" ? <Shuffle size={22} /> : <Palette size={22} />}
              </div>
              <div>
                <strong>{item.title}</strong>
                <small>{item.category} · {item.date}</small>
              </div>
              <span className="like-pill"><Heart size={15} /> {item.likes}</span>
            </article>
          ))}
        </div>
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
