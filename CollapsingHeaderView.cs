
using Foundation;
using System;
using UIKit;
using System.Collections.Generic;
using CoreGraphics;

namespace Project.iOS
{
	public enum MGTransformCurve
	{
		Linear = 0,
		EaseIn,
		EaseOut,
		EaseInOut,
	}

	//Enumeration of attributes that can be transformed when scrolling.
	public enum MGAttribute
	{
		MGAttributeX = 1,
		MGAttributeY,
		MGAttributeWidth,
		MGAttributeHeight,
		MGAttributeAlpha,
		MGAttributeCornerRadius,
		MGAttributeShadowRadius,
		MGAttributeShadowOpacity,
		MGAttributeFontSize
	}

	//Defines an attribute to be transformed when scrolling.
	public class MGTransform
	{
		public MGAttribute Attribute;
		public MGTransformCurve Curve;
		public float Value, OrigValue;

		public MGTransform TransformAttribute(MGAttribute attr, float val)
		{
			MGTransform a = new MGTransform();
			a.Attribute = attr;
			a.Value = val;
			a.Curve = MGTransformCurve.Linear;

			return a;
		}
	}

	public partial class CollapsingHeaderView : UIView
	{
		float MinimumHeaderHeight { get; set; }
		public bool AlwaysCollapse { get; set; }

		List<NSLayoutConstraint> hdrConstrs;
		List<nfloat> hdrConstrVals;
		List<UIView> transfViews, fadeViews;
		Dictionary<string, List<MGTransform>> transfAttrs;
		Dictionary<string, nfloat> alphaRatios;
		Dictionary<string, Dictionary<string, nfloat>> constrVals;
		Dictionary<string, Dictionary<string, NSLayoutConstraint>> constrs;
		Dictionary<NSLayoutAttribute, bool> vertConstraints;
		nfloat lastOffset;
		nfloat header_ht, scroll_ht, offset_max;
		UIFont font;

		public CollapsingHeaderView()
		{
			InitView();
		}

		public CollapsingHeaderView(NSCoder coder) : base(coder)
		{
			InitView();
		}

		public CollapsingHeaderView(NSObjectFlag t) : base(t)
		{
			InitView();
		}

		public CollapsingHeaderView(CGRect frame) : base(frame)
		{
			InitView();
		}

		public CollapsingHeaderView(IntPtr handle) : base(handle)
		{
			InitView();
		}

		private void InitView()
		{
			transfViews = new List<UIView>();
			fadeViews = new List<UIView>();
			transfAttrs = new Dictionary<string, List<MGTransform>>();
			constrs = new Dictionary<string, Dictionary<string, NSLayoutConstraint>>();
			constrVals = new Dictionary<string, Dictionary<string, nfloat>>();
			alphaRatios = new Dictionary<string, nfloat>();
			vertConstraints = new Dictionary<NSLayoutAttribute, bool>();
			vertConstraints.Add(NSLayoutAttribute.Top, true);
			vertConstraints.Add(NSLayoutAttribute.TopMargin, true);
			vertConstraints.Add(NSLayoutAttribute.Bottom, true);
			vertConstraints.Add(NSLayoutAttribute.BottomMargin, true);

			header_ht = Frame.Size.Height;
			scroll_ht = -1.0f;

			SetMinimumHeaderHeight(60);
			AlwaysCollapse = true;
		}

		public void SetMinimumHeaderHeight(float minimumHeaderHeight)
		{
			MinimumHeaderHeight = minimumHeaderHeight;
			offset_max = header_ht - MinimumHeaderHeight;
		}

		public void SetCollapsingConstraint(NSLayoutConstraint c)
		{
			SetCollapsingConstraints(new List<NSLayoutConstraint> { c });
		}

		private void SetCollapsingConstraints(List<NSLayoutConstraint> cs)
		{
			hdrConstrs = cs;
			var vals = new List<nfloat>();

			foreach (var c in cs) {

				vals.Add(c.Constant);
			}
			hdrConstrVals = vals;
		}

		public void CollapseWithScroll(UIScrollView scrollView)
		{
			nfloat dy = scrollView.ContentOffset.Y;
			if (scroll_ht < 0.0f) {
				scroll_ht = scrollView.Frame.Size.Height;
			}
			nfloat scrollableHeight = scrollView.ContentSize.Height - scroll_ht;

			if (scrollableHeight / 2.0 < offset_max) {
				if (AlwaysCollapse == true) {
					UIEdgeInsets scrInset = scrollView.ContentInset;
					scrInset.Bottom = 2.0f * offset_max - scrollableHeight;
					scrollView.ContentInset = scrInset;
				} else {
					return;
				}
			}

			if (dy > 0.0f) {
				if (header_ht - dy > MinimumHeaderHeight) {
					ScrollHeaderToOffset(dy);
				} else if (header_ht - lastOffset > MinimumHeaderHeight) {
					ScrollHeaderToOffset(offset_max);
				}
			} else if (lastOffset > 0.0f) {
				ScrollHeaderToOffset(0.0f);
			}

			SetNeedsUpdateConstraints();
			SetNeedsLayout();
			LayoutIfNeeded();

			lastOffset = dy;
		}

		private bool AddTransformingSubview(UIView view, List<MGTransform> transforms)
		{
			var constrDict = new Dictionary<string, NSLayoutConstraint>();
			var constrValDict = new Dictionary<string, nfloat>();

			while (view != null) {
				foreach (var constraints in view.Constraints) {
					if (constraints.FirstItem == view) {

						constrDict.Add(constraints.FirstAttribute.ToString(), constraints);
						constrValDict.Add(constraints.FirstAttribute.ToString(), constraints.Constant);

					} else if (constraints.SecondItem == view) {

						constrDict.Add(constraints.SecondAttribute.ToString(), constraints);
						constrValDict.Add(constraints.SecondAttribute.ToString(), constraints.Constant);
					}
				}
				view = view.Superview;
			}

			foreach (MGTransform transform in transforms) {
				transform.OrigValue = (float)GetViewAttribute(transform.Attribute, view);
			}

			transfViews.Add(view);
			transfAttrs.Add(view.GetHashCode().ToString(), transforms);
			constrs.Add(view.GetHashCode().ToString(), constrDict);
			constrVals.Add(view.GetHashCode().ToString(), constrValDict);

			return true;
		}

		private bool AddFadingSubview(UIView view, nfloat ratio)
		{
			if (ratio < 0.0f || ratio > 1.0f) {
				return false;
			}

			fadeViews.Add(view);
			alphaRatios.Add(view.GetHashCode().ToString(), ratio);

			return true;
		}

		private void ScrollHeaderToOffset(nfloat offset)
		{
			nfloat thisRatio = offset / offset_max;

			foreach (var view in fadeViews) {
				nfloat alphaRatio = alphaRatios[view.GetHashCode().ToString()];
				view.Alpha = -thisRatio / alphaRatio + 1;
			}

			foreach (var view in transfViews) {
				Dictionary<string, NSLayoutConstraint> cs = constrs[view.GetHashCode().ToString()];
				Dictionary<string, nfloat> cvs = constrVals[view.GetHashCode().ToString()];
				List<MGTransform> attributeSet = transfAttrs[view.GetHashCode().ToString()];

				foreach (var transform in attributeSet) {
					SetAttribute(transform, view, thisRatio, cs, cvs);
				}
			}

			CGRect hdrFrame = Frame;
			hdrFrame.Y = -offset;
			Frame = hdrFrame;

			for (int i = 0; i < hdrConstrs.Count; i++) {
				hdrConstrs[i].Constant = hdrConstrVals[i] - offset;
			}
		}

		private void UpdateConstraint(NSLayoutConstraint constraint, nfloat constraintValue, MGTransform transform, nfloat thisRatio)
		{
			if (constraint != null) {
				switch (constraint.FirstAttribute) {
					case NSLayoutAttribute.Top:
					case NSLayoutAttribute.TopMargin:
					case NSLayoutAttribute.Leading:
					case NSLayoutAttribute.LeadingMargin:
					case NSLayoutAttribute.Width:
					case NSLayoutAttribute.Height:
						constraint.Constant = constraintValue + thisRatio * transform.Value;
						break;
					case NSLayoutAttribute.Bottom:
					case NSLayoutAttribute.BottomMargin:
					case NSLayoutAttribute.Trailing:
					case NSLayoutAttribute.TrailingMargin:
						constraint.Constant = constraintValue - thisRatio * transform.Value;
						break;
					default:
						break;
				}
			}
		}

		private nfloat GetViewAttribute(MGAttribute attribute, UIView view)
		{
			switch (attribute) {
				case MGAttribute.MGAttributeX:
					return view.Frame.X;
				case MGAttribute.MGAttributeY:
					return view.Frame.Y;
				case MGAttribute.MGAttributeWidth:
					return view.Frame.Size.Width;
				case MGAttribute.MGAttributeHeight:
					return view.Frame.Size.Height;
				case MGAttribute.MGAttributeAlpha:
					return view.Alpha;
				case MGAttribute.MGAttributeCornerRadius:
					return view.Layer.CornerRadius;
				case MGAttribute.MGAttributeShadowOpacity:
					return view.Layer.ShadowOpacity;
				case MGAttribute.MGAttributeShadowRadius:
					return view.Layer.ShadowRadius;
				case MGAttribute.MGAttributeFontSize:
					if (view is UILabel) {
						return ((UILabel)view).Font.PointSize;
					} else if (view is UIButton) {
						return ((UIButton)view).TitleLabel.Font.PointSize;
					} else if (view is UITextField) {
						return ((UITextField)view).Font.PointSize;
					} else if (view is UITextView) {
						return ((UITextView)view).Font.PointSize;
					}
					return 0.0f;
				default:
					return 0.0f;
			}
		}

		private void SetAttribute(MGTransform attr, UIView view, nfloat thisRatio, Dictionary<string, NSLayoutConstraint> cs, Dictionary<string, nfloat> cvals)
		{
			switch (attr.Attribute) {
				case MGAttribute.MGAttributeX:
					UpdateConstraint(cs[NSLayoutAttribute.Leading.ToString()], cvals[NSLayoutAttribute.Leading.ToString()], attr, thisRatio);
					UpdateConstraint(cs[NSLayoutAttribute.LeadingMargin.ToString()], cvals[NSLayoutAttribute.LeadingMargin.ToString()], attr, thisRatio);
					UpdateConstraint(cs[NSLayoutAttribute.Trailing.ToString()], cvals[NSLayoutAttribute.Trailing.ToString()], attr, thisRatio);
					UpdateConstraint(cs[NSLayoutAttribute.TrailingMargin.ToString()], cvals[NSLayoutAttribute.TrailingMargin.ToString()], attr, thisRatio);
					break;
				case MGAttribute.MGAttributeY:
					UpdateConstraint(cs[NSLayoutAttribute.Top.ToString()], cvals[NSLayoutAttribute.Top.ToString()], attr, thisRatio);
					UpdateConstraint(cs[NSLayoutAttribute.TopMargin.ToString()], cvals[NSLayoutAttribute.TopMargin.ToString()], attr, thisRatio);
					UpdateConstraint(cs[NSLayoutAttribute.Bottom.ToString()], cvals[NSLayoutAttribute.Bottom.ToString()], attr, thisRatio);
					UpdateConstraint(cs[NSLayoutAttribute.BottomMargin.ToString()], cvals[NSLayoutAttribute.BottomMargin.ToString()], attr, thisRatio);
					break;
				case MGAttribute.MGAttributeWidth:
					UpdateConstraint(cs[NSLayoutAttribute.Width.ToString()], cvals[NSLayoutAttribute.Width.ToString()], attr, thisRatio);
					break;
				case MGAttribute.MGAttributeHeight:
					UpdateConstraint(cs[NSLayoutAttribute.Height.ToString()], cvals[NSLayoutAttribute.Height.ToString()], attr, thisRatio);
					break;
				case MGAttribute.MGAttributeCornerRadius:
					view.Layer.CornerRadius = attr.OrigValue + thisRatio * attr.Value;
					break;
				case MGAttribute.MGAttributeAlpha:
					view.Alpha = attr.OrigValue + thisRatio * attr.Value;
					break;
				case MGAttribute.MGAttributeShadowRadius:
					view.Layer.ShadowRadius = attr.OrigValue + thisRatio * attr.Value;
					break;
				case MGAttribute.MGAttributeShadowOpacity:
					view.Layer.ShadowOpacity = (float)(attr.OrigValue + thisRatio * attr.Value);
					break;
				case MGAttribute.MGAttributeFontSize:
					if (view is UILabel) {
						font = UIFont.FromName(((UILabel)view).Font.FamilyName, attr.OrigValue + thisRatio * attr.Value);
						((UILabel)view).Font = font;
					} else if (view is UIButton) {
						font = UIFont.FromName(((UIButton)view).TitleLabel.Font.FamilyName, attr.OrigValue + thisRatio * attr.Value);
						((UIButton)view).TitleLabel.Font = font;
					} else if (view is UITextField) {
						font = UIFont.FromName(((UITextField)view).Font.FamilyName, attr.OrigValue + thisRatio * attr.Value);
						((UITextField)view).Font = font;
					} else if (view is UITextView) {
						font = UIFont.FromName(((UITextView)view).Font.FamilyName, attr.OrigValue + thisRatio * attr.Value);
						((UITextView)view).Font = font;
					}
					break;
			}
		}
	}
}