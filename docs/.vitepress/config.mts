import { defineConfig } from 'vitepress'
import { withMermaid } from 'vitepress-plugin-mermaid'

export default withMermaid(
  defineConfig({
    title: 'VALYR Physics',
    description: 'High-Performance Modular Vehicle Simulation Framework for Unity',
    base: '/VALYR/',
    cleanUrls: true,
  themeConfig: {
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Quick Start', link: '/guide/quick-start' },
      { text: 'Architecture', link: '/guide/architecture' },
      { text: 'Modules', link: '/guide/wheel-contact' },
      { text: 'Presets', link: '/guide/presets' },
      { text: 'Multiplayer Addon', link: '/guide/multiplayer-addon' }
    ],
    sidebar: [
      {
        text: 'Getting Started',
        items: [
          { text: 'Overview', link: '/' },
          { text: 'Quick Start Guide', link: '/guide/quick-start' },
          { text: 'System Architecture', link: '/guide/architecture' }
        ]
      },
      {
        text: 'Vehicle Subsystems',
        items: [
          { text: 'Wheel Contact Detection', link: '/guide/wheel-contact' },
          { text: 'Suspension & Damper Dynamics', link: '/guide/suspension' },
          { text: 'Brush Tires & Assists (ARB/ABS/TCS)', link: '/guide/tires-and-assists' },
          { text: 'Powertrain & Steering', link: '/guide/powertrain-steering' },
          { text: 'Visual View & Telemetry HUD', link: '/guide/visual-and-telemetry' }
        ]
      },
      {
        text: 'Modular Addons',
        items: [
          { text: 'Multiplayer (PurrNet & PurrDiction)', link: '/guide/multiplayer-addon' }
        ]
      },
      {
        text: 'Development & Workflow',
        items: [
          { text: 'Presets & Custom Inspector', link: '/guide/presets' },
          { text: 'Conventional Commits & Semantic Release', link: '/guide/contributing' }
        ]
      }
    ],
    socialLinks: [
      { icon: 'github', link: 'https://github.com/l0nnes/VALYR' }
    ],
    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2026 Zakhar Czembrowicz (l0nnes)'
    },
    search: {
      provider: 'local'
    }
  }
}))

