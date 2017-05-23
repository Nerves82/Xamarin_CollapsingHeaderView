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
		X = 1,
		Y,
		Width,
		Height,
		Alpha,
		CornerRadius,
		ShadowRadius,
		ShadowOpacity,
		FontSize
	}

	//Defines an attribute to be transformed when scrolling.
	public class MGTransform
	{
		public MGAttribute Attribute;
		public MGTransformCurve Curve;
		public float Value, OrigValue;

		public static MGTransform TransformAttribute(MGAttribute attr, float val)
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
		public bool AlwaysCollapse { get; set; }

		List<NSLayoutConstraint> _headerConstraints;
		List<nfloat> _headerConstraintValues;
		List<UIView> _transfViews, _fadeOutViews, _fadeInViews;
		Dictionary<string, List<MGTransform>> _transfAttrs;
		Dictionary<string, nfloat> _alphaRatios;
		Dictionary<string, Dictionary<string, nfloat>> _constrVals;
		Dictionary<string, Dictionary<string, NSLayoutConstraint>> _constrs;
		Dictionary<NSLayoutAttribute, bool> _vertConstraints;
		nfloat _lastOffset;
		nfloat _headerHeight, _scrollHeight, _maxOffset;
		float _minimumHeaderHeight;

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
			_transfViews = new List<UIView>();
			_fadeOutViews = new List<UIView>();
			_fadeInViews = new List<UIView>();
			_transfAttrs = new Dictionary<string, List<MGTransform>>();
			_constrs = new Dictionary<string, Dictionary<string, NSLayoutConstraint>>();
			_constrVals = new Dictionary<string, Dictionary<string, nfloat>>();
			_alphaRatios = new Dictionary<string, nfloat>();
			_vertConstraints = new Dictionary<NSLayoutAttribute, bool>();
			_vertConstraints.Add(NSLayoutAttribute.Top, true);
			_vertConstraints.Add(NSLayoutAttribute.TopMargin, true);
			_vertConstraints.Add(NSLayoutAttribute.Bottom, true);
			_vertConstraints.Add(NSLayoutAttribute.BottomMargin, true);

			_headerHeight = Frame.Size.Height;
			_scrollHeight = -1.0f;

			SetMinimumHeaderHeight(60);
			AlwaysCollapse = true;
		}

		public void SetMinimumHeaderHeight(float minimumHeaderHeight)
		{
			_minimumHeaderHeight = minimumHeaderHeight;
			_maxOffset = _headerHeight - _minimumHeaderHeight;
		}

		public void SetCollapsingConstraint(NSLayoutConstraint c)
		{
			SetCollapsingConstraints(new List<NSLayoutConstraint> { c });
		}

		private void SetCollapsingConstraints(List<NSLayoutConstraint> cs)
		{
			_headerConstraints = cs;
			var vals = new List<nfloat>();

			foreach (var c in cs) {
				vals.Add(c.Constant);
			}
			_headerConstraintValues = vals;
		}

		public void CollapseWithScroll(UIScrollView scrollView)
		{
			nfloat y = scrollView.ContentOffset.Y;
			if (_scrollHeight < 0.0f) {
				_scrollHeight = scrollView.Frame.Size.Height;
			}
			nfloat scrollableHeight = scrollView.ContentSize.Height - _scrollHeight;

			if (scrollableHeight / 2.0 < _maxOffset) {
				if (AlwaysCollapse == true) {
					UIEdgeInsets scrInset = scrollView.ContentInset;
					scrInset.Bottom = 2.0f * _maxOffset - scrollableHeight;
					scrollView.ContentInset = scrInset;
				} else {
					return;
				}
			}

			if (y > 0.0f) {
				if (_headerHeight - y > _minimumHeaderHeight) {
					ScrollHeaderToOffset(y);
				} else if (_headerHeight - _lastOffset > _minimumHeaderHeight) {
					ScrollHeaderToOffset(_maxOffset);
				}
			} else if (_lastOffset > 0.0f) {
				ScrollHeaderToOffset(0.0f);
			}

			AnimateFadeInViews(y);

			SetNeedsUpdateConstraints();
			SetNeedsLayout();
			LayoutIfNeeded();

			_lastOffset = y;
		}

		public bool AddTransformingSubview(UIView view, List<MGTransform> transforms)
		{
			var constrDict = new Dictionary<string, NSLayoutConstraint>();
			var constrValDict = new Dictionary<string, nfloat>();

			UIView v = view;
			while (v != null) {
				foreach (var constraint in v.Constraints) {
					if (constraint.FirstItem == view) {

						if (constrDict.ContainsValue(constraint) == false) {
							constrDict.Add(constraint.FirstAttribute.ToString(), constraint);
							constrValDict.Add(constraint.FirstAttribute.ToString(), constraint.Constant);
						}

					} else if (constraint.SecondItem == view) {

						if (constrDict.ContainsValue(constraint) == false) {
							constrDict.Add(constraint.SecondAttribute.ToString(), constraint);
							constrValDict.Add(constraint.SecondAttribute.ToString(), constraint.Constant);
						}
					}
				}
				v = v.Superview;
			}

			foreach (MGTransform transform in transforms) {
				transform.OrigValue = (float)GetViewAttribute(transform.Attribute, view);
			}

			_transfViews.Add(view);
			_transfAttrs.Add(view.GetHashCode().ToString(), transforms);
			_constrs.Add(view.GetHashCode().ToString(), constrDict);
			_constrVals.Add(view.GetHashCode().ToString(), constrValDict);

			return true;
		}

		public bool AddFadeOutSubview(UIView view, nfloat ratio)
		{
			if (ratio < 0.0f || ratio > 1.0f) {
				return false;
			}

			_fadeOutViews.Add(view);
			_alphaRatios.Add(view.GetHashCode().ToString(), ratio);

			return true;
		}

		public void AddFadeInSubview(UIView view)
		{
			view.Alpha = 0f;
			_fadeInViews.Add(view);
		}

		private void AnimateFadeInViews(nfloat offset)
		{
			foreach (var view in _fadeInViews) {

				if (_headerHeight - offset > _minimumHeaderHeight) {
					view.Alpha = 0f;
				} else {
					var a = (offset / 1000) * 3f;
					view.Alpha = a;
				}
			}
		}

		private void ScrollHeaderToOffset(nfloat offset)
		{
			nfloat thisRatio = offset / _maxOffset;

			foreach (var view in _fadeOutViews) {
				nfloat alphaRatio = _alphaRatios[view.GetHashCode().ToString()];
				view.Alpha = -thisRatio / alphaRatio + 1;
			}

			foreach (var view in _transfViews) {
				Dictionary<string, NSLayoutConstraint> cs = _constrs[view.GetHashCode().ToString()];
				Dictionary<string, nfloat> cvs = _constrVals[view.GetHashCode().ToString()];
				List<MGTransform> attributeSet = _transfAttrs[view.GetHashCode().ToString()];

				foreach (var transform in attributeSet) {
					SetAttribute(transform, view, thisRatio, cs, cvs);
				}
			}

			CGRect hdrFrame = Frame;
			hdrFrame.Y = -offset;
			Frame = hdrFrame;

			for (int i = 0; i < _headerConstraints.Count; i++) {
				_headerConstraints[i].Constant = _headerConstraintValues[i] - offset;
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
				case MGAttribute.X:
					return view.Frame.X;
				case MGAttribute.Y:
					return view.Frame.Y;
				case MGAttribute.Width:
					return view.Frame.Size.Width;
				case MGAttribute.Height:
					return view.Frame.Size.Height;
				case MGAttribute.Alpha:
					return view.Alpha;
				case MGAttribute.CornerRadius:
					return view.Layer.CornerRadius;
				case MGAttribute.ShadowOpacity:
					return view.Layer.ShadowOpacity;
				case MGAttribute.ShadowRadius:
					return view.Layer.ShadowRadius;
				case MGAttribute.FontSize:
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
				case MGAttribute.X:

					if (cs.ContainsKey(NSLayoutAttribute.Leading.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.Leading.ToString()], cvals[NSLayoutAttribute.Leading.ToString()], attr, thisRatio);

					if (cs.ContainsKey(NSLayoutAttribute.LeadingMargin.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.LeadingMargin.ToString()], cvals[NSLayoutAttribute.LeadingMargin.ToString()], attr, thisRatio);

					if (cs.ContainsKey(NSLayoutAttribute.Trailing.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.Trailing.ToString()], cvals[NSLayoutAttribute.Trailing.ToString()], attr, thisRatio);

					if (cs.ContainsKey(NSLayoutAttribute.TrailingMargin.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.TrailingMargin.ToString()], cvals[NSLayoutAttribute.TrailingMargin.ToString()], attr, thisRatio);

					break;
				case MGAttribute.Y:

					if (cs.ContainsKey(NSLayoutAttribute.Top.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.Top.ToString()], cvals[NSLayoutAttribute.Top.ToString()], attr, thisRatio);

					if (cs.ContainsKey(NSLayoutAttribute.TopMargin.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.TopMargin.ToString()], cvals[NSLayoutAttribute.TopMargin.ToString()], attr, thisRatio);

					if (cs.ContainsKey(NSLayoutAttribute.Bottom.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.Bottom.ToString()], cvals[NSLayoutAttribute.Bottom.ToString()], attr, thisRatio);

					if (cs.ContainsKey(NSLayoutAttribute.BottomMargin.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.BottomMargin.ToString()], cvals[NSLayoutAttribute.BottomMargin.ToString()], attr, thisRatio);

					break;
				case MGAttribute.Width:

					if (cs.ContainsKey(NSLayoutAttribute.Width.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.Width.ToString()], cvals[NSLayoutAttribute.Width.ToString()], attr, thisRatio);

					break;
				case MGAttribute.Height:

					if (cs.ContainsKey(NSLayoutAttribute.Height.ToString()))
						UpdateConstraint(cs[NSLayoutAttribute.Height.ToString()], cvals[NSLayoutAttribute.Height.ToString()], attr, thisRatio);

					break;
				case MGAttribute.CornerRadius:
					view.Layer.CornerRadius = attr.OrigValue + thisRatio * attr.Value;
					break;
				case MGAttribute.Alpha:
					view.Alpha = attr.OrigValue + thisRatio * attr.Value;
					break;
				case MGAttribute.ShadowRadius:
					view.Layer.ShadowRadius = attr.OrigValue + thisRatio * attr.Value;
					break;
				case MGAttribute.ShadowOpacity:
					view.Layer.ShadowOpacity = (float)(attr.OrigValue + thisRatio * attr.Value);
					break;
				case MGAttribute.FontSize:
					if (view is UILabel) {
						((UILabel)view).Font = ((UILabel)view).Font.WithSize(attr.OrigValue + thisRatio * attr.Value);
					} else if (view is UIButton) {
						((UIButton)view).Font = ((UIButton)view).Font.WithSize(attr.OrigValue + thisRatio * attr.Value);
					} else if (view is UITextField) {
						((UITextField)view).Font = ((UITextField)view).Font.WithSize(attr.OrigValue + thisRatio * attr.Value);
					} else if (view is UITextView) {
						((UITextView)view).Font = ((UITextView)view).Font.WithSize(attr.OrigValue + thisRatio * attr.Value);
					}
					break;
			}
		}
	}
}