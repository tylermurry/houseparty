export function initCursor() {
    const cursorElement = document.createElement('div');
    cursorElement.className = 'custom-cursor';
    document.body.appendChild(cursorElement);

    const handleMouseMove = (event: MouseEvent) => {
        cursorElement.style.transform = `translate3d(${event.clientX}px, ${event.clientY}px, 0)`;
    };

    window.addEventListener('mousemove', handleMouseMove);

    return {
        destroy() {
            window.removeEventListener('mousemove', handleMouseMove);
            cursorElement.remove();
        },
    };
}
