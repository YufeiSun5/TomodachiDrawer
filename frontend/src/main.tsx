import React, { useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  CheckCircle2,
  Download,
  FileImage,
  Github,
  ImageUp,
  Loader2,
  Play,
  ShieldCheck,
  SlidersHorizontal,
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

  function chooseFile(nextFile: File | null) {
    setError(null);
    setFile(nextFile);
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setPreviewUrl(nextFile ? URL.createObjectURL(nextFile) : null);
  }

  async function createJob() {
    if (!file) {
      setError("请先选择一张图片。");
      return;
    }

    setBusy(true);
    setError(null);

    try {
      const form = new FormData();
      form.append("image", file);
      form.append("boardType", boardType);
      form.append("outputType", outputType);
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
    <main className="app-shell">
      <header className="topbar">
        <div className="brand">
          <div className="brand-mark"><FileImage size={22} /></div>
          <div>
            <h1>TomodachiDrawer-CN</h1>
            <span>图片转绘画输入文件</span>
          </div>
        </div>
        <nav>
          <a href="https://github.com/Lucas7yoshi/TomodachiDrawer" target="_blank" rel="noreferrer">
            <Github size={18} /> 源码 / GPL-3.0
          </a>
          <span className="service-ok">服务状态：正常</span>
        </nav>
      </header>

      <section className="workspace">
        <Panel title="1. 上传图片" icon={<ImageUp size={18} />}>
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
            {previewUrl ? (
              <img src={previewUrl} alt="待生成图片预览" />
            ) : (
              <div className="dropzone-empty">
                <ImageUp size={40} />
                <strong>拖拽图片到此处，或点击选择文件</strong>
                <span>支持 PNG / JPG / WEBP，建议小于 1024 x 1024</span>
              </div>
            )}
          </label>

          {selectedFileMeta && (
            <div className="file-card">
              <div>
                <strong>{selectedFileMeta.name}</strong>
                <span>{selectedFileMeta.type} · {selectedFileMeta.size}</span>
              </div>
              <button className="icon-button danger" onClick={() => chooseFile(null)} aria-label="移除图片">
                <Trash2 size={16} />
              </button>
            </div>
          )}

          <div className="validation-row">
            <CheckCircle2 size={18} />
            <span>服务端会重新校验格式、大小并生成安全文件名。</span>
          </div>
        </Panel>

        <Panel title="2. 生成参数" icon={<SlidersHorizontal size={18} />}>
          <Field label="硬件板型">
            <Segmented value={boardType} onChange={setBoardType} options={["rp2040", "rp2350", "esp32-s3"]} />
          </Field>
          <Field label="输出类型">
            <Segmented value={outputType} onChange={setOutputType} options={["tdld", "uf2"]} />
          </Field>
          <Field label="调色方案">
            <select value={colourMatcher} onChange={(event) => setColourMatcher(event.target.value)}>
              <option value="arbitrary">自动匹配（推荐）</option>
              <option value="cielab">CIE Lab</option>
              <option value="redmean">Redmean</option>
              <option value="euclidean">Euclidean</option>
            </select>
          </Field>
          <Field label={`TSP 优化时间：${tspTimeLimit}s`}>
            <input
              type="range"
              min="10"
              max="120"
              step="10"
              value={tspTimeLimit}
              onChange={(event) => setTspTimeLimit(Number(event.target.value))}
            />
            <div className="range-labels"><span>快速</span><span>标准</span><span>高质量</span></div>
          </Field>

          {error && <div className="error-box">{error}</div>}

          <button className="primary-action" onClick={createJob} disabled={busy}>
            {busy ? <Loader2 className="spin" size={18} /> : <Play size={18} />}
            开始生成
          </button>
        </Panel>

        <Panel title="3. 任务状态" icon={<Loader2 size={18} />}>
          <div className="job-list">
            {jobs.length === 0 && <EmptyJobs />}
            {jobs.map((job) => (
              <article className={`job-card ${job.status}`} key={job.jobUuid}>
                <div>
                  <strong>{job.status === "success" ? "成功" : "排队中"}</strong>
                  <span>{job.fileName}</span>
                  <small>{job.boardType} · {job.outputType.toUpperCase()} · {job.colourMatcher}</small>
                </div>
                {job.downloadUrl ? (
                  <a className="download-button" href={`${apiBase}${job.downloadUrl}`}>
                    <Download size={16} /> 下载 {job.outputType.toUpperCase()}
                  </a>
                ) : (
                  <span className="queue-chip">位置 {job.queuePosition}</span>
                )}
              </article>
            ))}
          </div>
        </Panel>
      </section>

      <section className="gallery-band">
        <div>
          <h2>已通过作品（审核队列预览）</h2>
          <p>公开展示前必须经管理员审核，默认生成结果不公开。</p>
        </div>
        <div className="gallery-preview">
          {["山间小屋", "像素头像", "海边灯塔", "复古游戏机"].map((item) => (
            <div className="gallery-tile" key={item}>
              <div className="tile-art" />
              <span>{item}</span>
            </div>
          ))}
        </div>
        <div className="license-note">
          <ShieldCheck size={22} />
          <span>基于 TomodachiDrawer 构建，遵循 GPL-3.0 开源协议。</span>
        </div>
      </section>
    </main>
  );
}

function Panel({ title, icon, children }: { title: string; icon: React.ReactNode; children: React.ReactNode }) {
  return (
    <section className="panel">
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
      <Loader2 size={28} />
      <strong>等待生成任务</strong>
      <span>上传图片并点击开始生成后，任务会显示在这里。</span>
    </div>
  );
}

createRoot(document.getElementById("root")!).render(<App />);
