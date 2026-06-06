import React, { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  CheckCircle2,
  Download,
  FileImage,
  Github,
  Heart,
  Home,
  ImageUp,
  Loader2,
  Palette,
  Play,
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
  createdAt: string;
  downloadUrl?: string;
  message?: string;
};

const apiBase = import.meta.env.VITE_API_BASE ?? "";

function App() {
  const [file, setFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [boardType, setBoardType] = useState("rp2040");
  const [outputType, setOutputType] = useState("tdld");
  const [switchVersion, setSwitchVersion] = useState("switch2");
  const [colourMatcher, setColourMatcher] = useState("arbitrary");
  const [tspTimeLimit, setTspTimeLimit] = useState(30);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedFileMeta = useMemo(() => {
    if (!file) return null;
    return {
      name: file.name,
      size: `${Math.max(1, Math.round(file.size / 1024))} KB`,
      type: file.type || "unknown"
    };
  }, [file]);

  useEffect(() => {
    if (boardType === "esp32-s3" && outputType === "uf2") {
      setOutputType("tdld");
    }
  }, [boardType, outputType]);

  function chooseFile(nextFile: File | null) {
    setError(null);
    setFile(nextFile);
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setPreviewUrl(nextFile ? URL.createObjectURL(nextFile) : null);
  }

  async function createJob() {
    if (!file) {
      setError("请先把一张图片放到小屋画架里。");
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
    <main className="dream-shell">
      <header className="topbar">
        <div className="brand">
          <div className="brand-mark">
            <Home size={22} />
          </div>
          <div>
            <h1>TomodachiDrawer-CN</h1>
            <span>朋友收集梦想生活 · 画作转换小屋</span>
          </div>
        </div>
        <nav>
          <a href="https://github.com/Lucas7yoshi/TomodachiDrawer" target="_blank" rel="noreferrer">
            <Github size={18} /> 源码 / GPL-3.0
          </a>
          <span className="service-ok"><Sparkles size={16} /> 服务在线</span>
        </nav>
      </header>

      <section className="studio-banner">
        <div className="banner-copy">
          <h2>把一张图片放进小屋，生成绘画控制文件</h2>
          <p>当前是最小测试版：上传、参数、任务和下载链路已可验证；公开作品仍需管理员审核。</p>
        </div>
        <div className="mini-island" aria-hidden="true">
          <span className="sun" />
          <span className="house house-one" />
          <span className="house house-two" />
          <span className="tree" />
          <span className="cloud cloud-one" />
          <span className="cloud cloud-two" />
        </div>
      </section>

      <section className="workspace">
        <Panel title="上传到画架" icon={<ImageUp size={18} />} tone="mint">
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
            <div className="easel">
              {previewUrl ? (
                <img src={previewUrl} alt="待生成图片预览" />
              ) : (
                <div className="dropzone-empty">
                  <ImageUp size={42} />
                  <strong>点击或拖拽图片</strong>
                  <span>PNG / JPG / WEBP，建议 256x256 以内</span>
                </div>
              )}
            </div>
          </label>

          {selectedFileMeta && (
            <div className="file-card">
              <FileImage size={18} />
              <div>
                <strong>{selectedFileMeta.name}</strong>
                <span>{selectedFileMeta.type} · {selectedFileMeta.size}</span>
              </div>
              <button className="icon-button danger" onClick={() => chooseFile(null)} aria-label="移除图片">
                <Trash2 size={16} />
              </button>
            </div>
          )}

          <div className="note-row">
            <CheckCircle2 size={18} />
            <span>服务端会重新校验格式、大小并生成安全文件名。</span>
          </div>
        </Panel>

        <Panel title="绘画设置" icon={<SlidersHorizontal size={18} />} tone="peach">
          <Field label="小板住户">
            <Segmented value={boardType} onChange={setBoardType} options={["rp2040", "rp2350", "esp32-s3"]} />
          </Field>
          <Field label="主机节奏">
            <Segmented value={switchVersion} onChange={setSwitchVersion} options={["switch2", "switch1"]} />
          </Field>
          <Field label="带走的文件">
            <Segmented
              value={outputType}
              onChange={setOutputType}
              options={boardType === "esp32-s3" ? ["tdld"] : ["tdld", "uf2"]}
            />
            <small className="field-hint">
              RP2040/RP2350 可下载 UF2；ESP32-S3 先刷基础固件，再写入 TDLD。
            </small>
          </Field>
          <Field label="调色心情">
            <select value={colourMatcher} onChange={(event) => setColourMatcher(event.target.value)}>
              <option value="arbitrary">自动匹配（推荐）</option>
              <option value="cielab">柔和彩度 CIE Lab</option>
              <option value="redmean">复古色感 Redmean</option>
              <option value="euclidean">直接距离 Euclidean</option>
            </select>
          </Field>
          <Field label={`路线耐心：${tspTimeLimit}s`}>
            <input
              type="range"
              min="10"
              max="120"
              step="10"
              value={tspTimeLimit}
              onChange={(event) => setTspTimeLimit(Number(event.target.value))}
            />
            <div className="range-labels"><span>快画</span><span>细画</span><span>慢慢画</span></div>
          </Field>

          {error && <div className="error-box">{error}</div>}

          <button className="primary-action" onClick={createJob} disabled={busy}>
            {busy ? <Loader2 className="spin" size={18} /> : <Play size={18} />}
            开始生成
          </button>
        </Panel>

        <Panel title="小屋队列" icon={<Loader2 size={18} />} tone="sky">
          <div className="queue-house">
            <div className="queue-roof">任务电梯</div>
            <div className="job-list">
              {jobs.length === 0 && <EmptyJobs />}
              {jobs.map((job, index) => (
                <article className={`job-card ${job.status}`} key={job.jobUuid}>
                  <div className="room-number">{String(index + 1).padStart(2, "0")}</div>
                  <div>
                    <strong>{job.status === "success" ? "绘画包已准备好" : "正在排队"}</strong>
                    <span>{job.fileName}</span>
                    <small>{job.boardType} · {job.outputType.toUpperCase()} · {job.colourMatcher}</small>
                  </div>
                  {job.downloadUrl ? (
                    <a className="download-button" href={`${apiBase}${job.downloadUrl}`}>
                      <Download size={16} /> 下载
                    </a>
                  ) : (
                    <span className="queue-chip">位置 {job.queuePosition}</span>
                  )}
                </article>
              ))}
            </div>
          </div>
        </Panel>
      </section>

      <section className="gallery-band">
        <div className="gallery-heading">
          <Heart size={22} />
          <div>
            <h2>梦想生活作品墙</h2>
            <p>公开展示前必须经管理员审核，默认生成结果不公开。</p>
          </div>
        </div>
        <div className="gallery-preview">
          {["海边小屋", "圆圆头像", "午后甜点", "星星衬衫"].map((item, index) => (
            <div className="gallery-tile" key={item}>
              <div className={`tile-art tile-${index + 1}`}>
                <Palette size={20} />
              </div>
              <span>{item}</span>
            </div>
          ))}
        </div>
        <div className="license-note">
          <ShieldCheck size={22} />
          <span>基于 TomodachiDrawer 构建，遵循 GPL-3.0；本站与 Nintendo 无官方关联。</span>
        </div>
      </section>
    </main>
  );
}

function Panel({
  title,
  icon,
  tone,
  children
}: {
  title: string;
  icon: React.ReactNode;
  tone: "mint" | "peach" | "sky";
  children: React.ReactNode;
}) {
  return (
    <section className={`panel ${tone}`}>
      <h2>{icon}{title}</h2>
      {children}
    </section>
  );
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
  return (
    <div className="segmented">
      {options.map((option) => (
        <button
          type="button"
          className={option === value ? "selected" : ""}
          onClick={() => onChange(option)}
          key={option}
        >
          {option.toUpperCase()}
        </button>
      ))}
    </div>
  );
}

function EmptyJobs() {
  return (
    <div className="empty-jobs">
      <span className="empty-face">:)</span>
      <strong>还没有住户排队</strong>
      <span>上传图片并开始生成后，任务会搬进这里。</span>
    </div>
  );
}

createRoot(document.getElementById("root")!).render(<App />);
