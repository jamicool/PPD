import { createRouter, createWebHistory } from 'vue-router'
import DesignerView from '../views/DesignerView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'designer',
      component: DesignerView
    }
  ]
})

export default router
