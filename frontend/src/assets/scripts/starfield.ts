type Vec2 = { x: number; y: number };
type NonEmptyArray<T> = [T, ...T[]];

type Star = {
    x: number;
    y: number;
    depth: number;
    size: number;
    a: number;
    phase: number;
    pulse: number;
    plus: boolean;
};

let activeField: PixelStarField | null = null;

class PixelStarField {
    private container: HTMLElement;
    private canvas: HTMLCanvasElement;
    private ctx: CanvasRenderingContext2D;
    private w = 0;
    private h = 0;
    private dpr = 1;
    private stars: Star[] = [];
    private last = performance.now();
    private running = true;
    private frameId = 0;

    private starCount = 160;
    private depths: NonEmptyArray<number> = [0.35, 0.65, 1.0];
    private driftSpeed = 40;
    private driftDir: Vec2 = { x: 1, y: -1 };

    private camX = 0;
    private camY = 0;
    private camXTarget = 0;
    private camYTarget = 0;
    private ease = 0.08;
    private travelAnim: {
        startX: number;
        startY: number;
        deltaX: number;
        deltaY: number;
        startTime: number;
        duration: number;
    } | null = null;

    private resizeHandler = this.resize.bind(this);
    private frameHandler = this.frame.bind(this);

    constructor(targetId: string) {
        const container = document.getElementById(targetId);
        if (!container) {
            throw new Error(`PixelStarField: element '${targetId}' not found`);
        }

        this.container = container;
        this.canvas = document.createElement('canvas');
        Object.assign(this.canvas.style, {
            position: 'absolute',
            inset: '0',
            width: '100%',
            height: '100%',
            pointerEvents: 'none',
            imageRendering: 'pixelated',
        });

        if (!this.container.style.position) {
            this.container.style.position = 'relative';
        }
        this.container.appendChild(this.canvas);

        const ctx = this.canvas.getContext('2d');
        if (!ctx) {
            throw new Error('PixelStarField: unable to get 2D context');
        }
        this.ctx = ctx;

        window.addEventListener('resize', this.resizeHandler);
        this.resize();
        this.initStars();
        this.frameId = requestAnimationFrame(this.frameHandler);
    }

    destroy() {
        this.running = false;
        cancelAnimationFrame(this.frameId);
        window.removeEventListener('resize', this.resizeHandler);
        this.canvas.remove();
    }

    travel(dist: number, dir: Vec2) {
        this.camXTarget += (dir.x || 0) * dist;
        this.camYTarget += (dir.y || 0) * dist;
    }

    animateTravel(dist: number, dir: Vec2, duration = 1400) {
        this.travelAnim = {
            startX: this.camXTarget,
            startY: this.camYTarget,
            deltaX: (dir.x || 0) * dist,
            deltaY: (dir.y || 0) * dist,
            startTime: performance.now(),
            duration,
        };
    }

    private resize() {
        this.w = this.container.clientWidth;
        this.h = this.container.clientHeight;
        this.dpr = Math.max(1, Math.floor(window.devicePixelRatio || 1));
        this.canvas.width = this.w * this.dpr;
        this.canvas.height = this.h * this.dpr;
        this.ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    }

    private rnd(a: number, b: number) {
        return a + Math.random() * (b - a);
    }

    private pick<T>(arr: NonEmptyArray<T>) {
        const index = (Math.random() * arr.length) | 0;
        return arr[index] ?? arr[0];
    }

    private initStars() {
        this.stars = Array.from({ length: this.starCount }, () => {
            const depth = this.pick(this.depths);
            const size = depth < 0.5 ? 1 : depth < 0.9 ? 2 : 3;
            return {
                x: this.rnd(0, this.w),
                y: this.rnd(0, this.h),
                depth,
                size,
                a: this.rnd(0.4, 1.0),
                phase: this.rnd(0, Math.PI * 2),
                pulse: this.rnd(1.4, 3.0),
                plus: Math.random() < 0.15,
            };
        });
    }

    private wrap(s: Star, ox: number, oy: number) {
        const m = 20;
        if (s.x + ox < -m) s.x += this.w + m * 2;
        if (s.x + ox > this.w + m) s.x -= this.w + m * 2;
        if (s.y + oy < -m) s.y += this.h + m * 2;
        if (s.y + oy > this.h + m) s.y -= this.h + m * 2;
    }

    private frame(now: number) {
        if (!this.running) return;

        const dt = Math.min(0.05, (now - this.last) / 1000);
        this.last = now;

        if (this.travelAnim) {
            const elapsed = now - this.travelAnim.startTime;
            const t = Math.min(1, Math.max(0, elapsed / this.travelAnim.duration));
            const eased = t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
            this.camXTarget = this.travelAnim.startX + this.travelAnim.deltaX * eased;
            this.camYTarget = this.travelAnim.startY + this.travelAnim.deltaY * eased;
            if (t >= 1) {
                this.travelAnim = null;
            }
        }

        this.camX += (this.camXTarget - this.camX) * this.ease;
        this.camY += (this.camYTarget - this.camY) * this.ease;

        this.ctx.clearRect(0, 0, this.w, this.h);
        const dx = this.driftDir.x * this.driftSpeed * dt;
        const dy = this.driftDir.y * this.driftSpeed * dt;

        for (const s of this.stars) {
            s.x += dx * s.depth;
            s.y += dy * s.depth;
            const ox = this.camX * s.depth;
            const oy = this.camY * s.depth;
            this.wrap(s, ox, oy);

            const tw = 0.75 + 0.25 * Math.sin(s.phase + now * 0.001 * s.pulse);
            const a = Math.max(0, Math.min(1, s.a * tw));
            const x = (s.x + ox) | 0;
            const y = (s.y + oy) | 0;
            const z = s.size;

            this.ctx.fillStyle = `rgba(255,255,255,${a.toFixed(3)})`;
            this.ctx.fillRect(x, y, z, z);
            if (s.plus) {
                this.ctx.fillRect(x - z, y, z, z);
                this.ctx.fillRect(x + z, y, z, z);
                this.ctx.fillRect(x, y - z, z, z);
                this.ctx.fillRect(x, y + z, z, z);
            }
        }

        this.frameId = requestAnimationFrame(this.frameHandler);
    }
}

export function initStarfield(targetId = 'starfield') {
    const field = new PixelStarField(targetId);
    activeField = field;
    const keyMap = new Set<string>();
    const SPEED = 900;
    let keyboardFrameId = 0;

    const onKeyDown = (e: KeyboardEvent) => {
        if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key)) {
            keyMap.add(e.key);
            e.preventDefault();
        }
    };

    const onKeyUp = (e: KeyboardEvent) => {
        keyMap.delete(e.key);
    };

    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);

    const keyboardFrame = () => {
        let dx = 0;
        let dy = 0;
        if (keyMap.has('ArrowUp')) dy += 1;
        if (keyMap.has('ArrowDown')) dy -= 1;
        if (keyMap.has('ArrowLeft')) dx += 1;
        if (keyMap.has('ArrowRight')) dx -= 1;

        if (dx !== 0 || dy !== 0) {
            const len = Math.hypot(dx, dy) || 1;
            field.travel(SPEED / 60, { x: dx / len, y: dy / len });
        }

        keyboardFrameId = requestAnimationFrame(keyboardFrame);
    };

    keyboardFrame();

    return {
        destroy() {
            cancelAnimationFrame(keyboardFrameId);
            window.removeEventListener('keydown', onKeyDown);
            window.removeEventListener('keyup', onKeyUp);
            field.destroy();
            if (activeField === field) {
                activeField = null;
            }
        },
    };
}
export function travelStarfieldUp(distance = 520) {
    activeField?.animateTravel(distance, { x: 0, y: -1 });
}

export function travelStarfieldDown(distance = 520) {
    activeField?.animateTravel(distance, { x: 0, y: 1 });
}
