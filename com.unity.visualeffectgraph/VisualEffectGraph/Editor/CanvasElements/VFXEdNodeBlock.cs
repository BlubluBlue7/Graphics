using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor.Experimental;
using UnityEditor.Experimental.Graph;
using Object = UnityEngine.Object;

namespace UnityEditor.Experimental
{
    internal abstract class VFXEdNodeBlock : CanvasElement
    {
        public string name{ get { return m_Name; } }
        protected string m_Name;
        protected VFXEdNodeBlockParameterField[] m_Fields;

        protected VFXEdDataSource m_DataSource;


        public VFXEdNodeBlock(VFXEdDataSource dataSource)
        {
            m_DataSource = dataSource;
            translation = Vector3.zero; // zeroed by default, will be relayouted later.
            m_Caps = Capabilities.Normal;

        }

        public bool IsConnected()
        {
            foreach(VFXEdNodeBlockParameterField field in m_Fields)
            {
                if (field.IsConnected())
                    return true;
            }
            return false;
        }

        // Retrieve the full height of the block
        public virtual float GetHeight()
        {
            float height = VFXEditorMetrics.NodeBlockHeaderHeight;
            if(!collapsed)
            {
                foreach(VFXEdNodeBlockParameterField field in m_Fields) {
                    height += field.scale.y + VFXEditorMetrics.NodeBlockParameterSpacingHeight;
                }
                height += VFXEditorMetrics.NodeBlockFooterHeight;
            }
            return height;
        }

        public override void Layout()
        {
            base.Layout();

            if (collapsed)
            {
                scale = new Vector2(scale.x, VFXEditorMetrics.NodeBlockHeaderHeight);

                // if collapsed, rejoin all connectors on the middle of the header
                foreach(VFXEdNodeBlockParameterField field in m_Fields)
                {
                    field.translation = new Vector2(0.0f, (VFXEditorMetrics.NodeBlockHeaderHeight-VFXEditorMetrics.DataAnchorSize.y)/2);
                }
            }
            else
            {
                scale = new Vector2(scale.x, GetHeight());
                float curY = VFXEditorMetrics.NodeBlockHeaderHeight;

                foreach(VFXEdNodeBlockParameterField field in m_Fields)
                {
                    field.translation = new Vector2(0.0f, curY);
                    curY += field.scale.y + VFXEditorMetrics.NodeBlockParameterSpacingHeight;
                }

            }
        }

        public bool IsSelectedNodeBlock(VFXEdCanvas canvas)
        {
            if (parent is VFXEdNodeBlockContainer)
            {
                return canvas.SelectedNodeBlock == this;
            }
            else
            {
                return false;
            }
        }



        protected abstract GUIStyle GetNodeBlockSelectedStyle();
        protected abstract GUIStyle GetNodeBlockStyle();



    }
}

