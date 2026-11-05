import { useEffect, useRef } from 'react'

/** The window scroll is the only source of progress. Floats are independent CSS. */
export function ProgressPath() {
  const root = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const section = root.current!
    const svg = section.querySelector('svg')!
    const paths = Array.from(svg.querySelectorAll('path'))
    const lit = svg.querySelector<SVGPathElement>('.path-lit')!
    const head = svg.querySelector('circle')!
    const nodes = Array.from(section.querySelectorAll<HTMLElement>('[data-stop]'))
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)')
    let length = 0, progress = 0, target = 0, frame = 0
    let fractions = [0, 1 / 3, 2 / 3, 1]
    function paint() {
      if (!length) return
      lit.style.strokeDashoffset = String(1000 * (1 - progress))
      const point = lit.getPointAtLength(length * progress)
      head.setAttribute('cx', String(point.x)); head.setAttribute('cy', String(point.y))
      section.dataset.progress = progress.toFixed(3)
      nodes.forEach((node, i) => {
        const reached = progress + .008 >= fractions[i]
        node.dataset.reached = String(reached)
        node.querySelector('b')!.textContent = reached ? '✓' : String(i + 1)
      })
    }
    function tick() { frame = 0; progress += (target - progress) * .105; if (Math.abs(target - progress) < .001) progress = target; paint(); if (progress !== target) frame = requestAnimationFrame(tick) }
    function sync() {
      target = Math.min(1, Math.max(0, window.scrollY / Math.max(1, document.documentElement.scrollHeight - window.innerHeight)))
      if (reduced.matches) { cancelAnimationFrame(frame); frame = 0; progress = target; paint() }
      else if (!frame) frame = requestAnimationFrame(tick)
    }
    function draw() {
      const w = section.clientWidth, h = section.clientHeight
      if (!w) return
      svg.setAttribute('viewBox', `0 0 ${w} ${h}`)
      const narrow = w < 500
      const points = narrow ? [[w * .22, 42], [w * .78, 42], [w * .78, 150], [w * .22, 150]] : [[w * .1, 65], [w * .36, 38], [w * .64, 65], [w * .9, 38]]
      const [a, b, c, d] = points
      const data = narrow ? `M ${a} L ${b} C ${w * .96} ${b[1]} ${w * .96} ${c[1]} ${c} L ${d}` : `M ${a} C ${w * .23} ${a[1]} ${w * .23} ${b[1]} ${b} S ${w * .51} ${c[1]} ${c} S ${w * .77} ${d[1]} ${d}`
      paths.forEach(path => path.setAttribute('d', data)); length = lit.getTotalLength()
      fractions = points.map(([x, y]) => { let best = Infinity, position = 0; for (let j = 0; j <= 600; j++) { const p = lit.getPointAtLength(length * j / 600), distance = (p.x - x) ** 2 + (p.y - y) ** 2; if (distance < best) { best = distance; position = j / 600 } } return position })
      nodes.forEach((node, i) => { node.style.left = points[i][0] + 'px'; node.style.top = points[i][1] + 'px' })
      sync(); paint()
    }
    const observer = new ResizeObserver(draw); observer.observe(section)
    window.addEventListener('scroll', sync, { passive: true }); window.addEventListener('resize', draw); reduced.addEventListener('change', sync)
    draw()
    return () => { observer.disconnect(); cancelAnimationFrame(frame); window.removeEventListener('scroll', sync); window.removeEventListener('resize', draw); reduced.removeEventListener('change', sync) }
  }, [])
  return <div ref={root} className="progress-path" aria-label="Recorrido: evidencia, GitHub, encaje y decisión"><svg aria-hidden="true"><path className="path-shadow" /><path className="path-edge" /><path className="path-paper" /><path className="path-lit" pathLength="1000" /><circle r="7" /></svg>{['Tu evidencia', 'Señales GitHub', 'El encaje', 'Tu decisión'].map((name, i) => <div className="path-stop" data-stop={i} key={name}><b aria-hidden="true">{i + 1}</b><span>{name}</span></div>)}</div>
}
