# Frontend Layering Standard

This document defines the standard pattern for separating responsibilities in the frontend.
The goal is to keep views declarative, move core logic into composables and services, and keep
animations/timers local to views when they are view-specific.

## Principles
- Views are UI-only: minimal state, layout, and composition of composables.
- Composables orchestrate feature logic: lifecycle hooks, side effects, and state updates.
- Services wrap API/SDK calls and response normalization.
- Components are presentational: props in, events out.
- Keep view-specific animations/timers in the view.

## What Goes Where
**Views**
- Route state, template structure, transitions, view-only timers/animations.
- Call composables and wire them to template.

**Composables**
- Own orchestration and side effects.
- Use `onMounted/onBeforeUnmount/watch` as needed.
- Expose reactive state + actions to the view.

**Services**
- Pure API wrappers and response normalization helpers.
- No Vue refs, no DOM access, no lifecycle hooks.

## Example: Service Layer
`src/services/taskService.ts`
```ts
export type Task = { id: string; title: string; done: boolean }

export async function fetchTasks() {
  const response = await fetch('/api/tasks')
  return (await response.json()) as Task[]
}

export async function createTask(title: string) {
  const response = await fetch('/api/tasks', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ title }),
  })
  return (await response.json()) as Task
}
```

## Example: Composable Layer
`src/composables/useTasks.ts`
```ts
import { onMounted, ref } from 'vue'
import { createTask, fetchTasks, type Task } from '@/services/taskService'

export function useTasks() {
  const tasks = ref<Task[]>([])
  const isLoading = ref(false)
  const error = ref('')

  async function loadTasks() {
    isLoading.value = true
    error.value = ''
    try {
      tasks.value = await fetchTasks()
    } catch {
      error.value = 'Failed to load tasks.'
    } finally {
      isLoading.value = false
    }
  }

  async function addTask(title: string) {
    const trimmed = title.trim()
    if (!trimmed) return
    const created = await createTask(trimmed)
    tasks.value = [created, ...tasks.value]
  }

  onMounted(() => {
    void loadTasks()
  })

  return { tasks, isLoading, error, loadTasks, addTask }
}
```

## Example: View Layer
`src/views/TasksView.vue` (logic excerpt)
```ts
import { ref } from 'vue'
import { useTasks } from '@/composables/useTasks'

const newTitle = ref('')
const { tasks, isLoading, error, addTask } = useTasks()

function submit() {
  void addTask(newTitle.value)
  newTitle.value = ''
}
```

## Conventions
- Composables are named `useXxx` and return `{ state, actions }`.
- Services use explicit verbs (`createRoom`, `joinRoom`, `negotiateConnection`).
- Keep view-specific animation/timer details in the view.
- Avoid putting DOM access or lifecycle hooks in services.

## Quick Checklist
- Is this logic view-only UI timing/animation? -> Keep in view.
- Does it orchestrate feature behavior? -> Composable.
- Is it just calling APIs or normalizing responses? -> Service.
